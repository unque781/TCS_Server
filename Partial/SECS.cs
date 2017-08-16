using Floware.Concurrent;
using Floware.MQ;
using Floware.SECSFlow.Core;
using Floware.SECSFlow.Core.Object;
using Floware.Utils;
using System.Linq;
using Floware.LINQ;
using System;
using System.Collections.Generic;
using TcsV2.Core;
using TcsV2.Core.MODEL;
using Floware.HSMS;

namespace TCS.ServerV2
{
    partial class FormMain
    {
        readonly string VER_SECS = "1.15.4.14";

        HsmsManager mgr = new HsmsManager();
        List<string> cmdList = new List<string>();

        /// <summary>
        /// 1 = Eqp Offline
        /// 2 = Attempt Online
        /// 3 = Host Offine
        /// 4 = Local
        /// 5 = Remote
        /// </summary>
        /// <returns></returns>

        int GetControlMode()
        {
            var v = (E_CONTROLSTATE)Enum.Parse( typeof( E_CONTROLSTATE ), sql.HsmsConfig.ControlMode.Value, true );

            return EnumUtils.IntV( v );
        }

        void InitHsms()
        {
            mgr.OnHsmsContd += (mgr_OnConnect);
            mgr.OnHsmsDiscontd += (mgr_OnDisconnect);
            mgr.OnHsmsRecd += (mgr_OnReceive);
            mgr.OnHsmsSent += (mgr_OnSendComplete);
            mgr.OnHsmsLog += (mgr_OnLog);
            mgr.OnHsmsT3Timeout += (mgr_OnTimeout);

            var hostConfig = sql.HsmsConfig.All;

            mgr.Config.IpAddress = hostConfig.FirstOrDefault( x => x.ID == E_HOST_CONFIG.HSMS_IP.ToString() ).Value;
            mgr.Config.Port = hostConfig.FirstOrDefault( x => x.ID == E_HOST_CONFIG.HSMS_PORT.ToString() ).IntValue;
            mgr.Config.DeviceID = hostConfig.FirstOrDefault( x => x.ID == E_HOST_CONFIG.HSMS_DEVICEID.ToString() ).IntValue;
            mgr.Config.HideLogLink = true;
            mgr.ReadMsgLibrary( "Config/tcs.sfm" );
            mgr.Connect( false, 10000 );

            SetcmdList();
        }

        private void SetcmdList()
        {
            cmdList.Add( "TRANSFER" );
            cmdList.Add( "CommandUpdate" );
            cmdList.Add( "REMOVE" );
            cmdList.Add( "ABORT" );
            cmdList.Add( "CANCEL" );
            cmdList.Add( "PAUSE" );
            cmdList.Add( "RESUME" );
        }
        //msg 별로 
        void PoolSecsRecd( SFMessage recd )
        {
            try
            {
                logger.I( string.Format( "{0} : {1}", "[MCS->TCS]", recd.LogFormat() ) );
                //logger.I(recd.LogFormat());

                if ( recd.IsSecondary )
                    return;

                //전처리 ==============================
                switch ( recd.SxFx )
                {
                    case "S1F1":
                        MsgS1F1( recd );
                        if ( GetControlMode() <= 3 ) return;
                        break;

                    case "S1F13":
                        MsgS1F13( recd );
                        if ( GetControlMode() <= 3 ) return;
                        break;

                    case "S1F15":
                        MsgS1F15( recd );
                        if ( GetControlMode() <= 3 ) return;
                        break;

                    case "S1F17":
                        MsgS1F17( recd );
                        if ( GetControlMode() <= 3 ) return;
                        break;

                    case "S6F12":
                        if ( GetControlMode() <= 3 ) return;
                        break;
                }
                //전처리 ==============================
                if ( GetControlMode() <= 3 )
                {
                    ReplyHsms( MakeAbortMsg( recd ) );
                    return;
                }

                switch ( recd.SxFx )
                {
                    case "S1F3":
                        MsgS1F3( recd );
                        break;

                    case "S2F17":
                        MsgS2F17( recd );
                        break;

                    case "S2F31":
                        MsgS2F31( recd );
                        break;

                    case "S2F41":
                        MsgS2F41( recd );
                        break;

                    case "S2F49":
                        MsgS2F49( recd );
                        break;

                    case "S5F5":
                        MsgS5F5( recd );
                        break;
                }
            }
            catch ( Exception e )
            {
                logger.E( "[SECS] " + e.Message );
            }
        }
        private SFMessage MakeAbortMsg( SFMessage recd )
        {
            SFMessage rep = new SFMessage( recd.Stream, 0 );
            rep.Name = "AbortMessage";
            rep.Systembyte = recd.Systembyte;
            return rep;
        }

        private void MsgS1F3( SFMessage recd )
        {
            int vidCount = recd["SVIDCOUNT"].IntValue;

            if ( vidCount > 1 )
            {
                HandleS1F3Init( recd );
            }
            else
            {
                try
                {
                    SFMessage rep = mgr["SSD-Tsc"];
                    rep["TSCState"].IntValue = sql.HsmsConfig.ControlState.ControlStateIntValue;
                    rep.Systembyte = recd.Systembyte;
                    ReplyHsms( rep );
                }
                catch ( Exception e )
                {
                    logger.E( "[SECS] " + e.Message );
                }
            }
        }

        private void MsgS2F17( SFMessage recd )
        {
            SFMessage rep = mgr["DTD"];
            rep["TIME"].Value = DateTime.Now.ToString( "YYYYMMDDhhmmsscc" );
            rep.Systembyte = recd.Systembyte;
            ReplyHsms( rep );
        }

        private void MsgS2F31( SFMessage recd )
        {
            string time = recd["TIME"].Value;
            DateUtils.ChangeSystemTime( time );
            SFMessage rep = mgr["DTA"];

            rep.Systembyte = recd.Systembyte;
            rep["TIACK"].IntValue = 0;
            ReplyHsms( rep );
        }

        private void MsgS2F41( SFMessage recd )
        {
            string sfName = recd.Name;
            string cmd = recd[0, 0].Value.Trim();

            if ( !cmdList.Contains( cmd ) )
            {
                ReplyS2F42( recd, E_HCACK.CMD_NOT_EXIST, "", E_CPACK.CPNAME_NOT_EXIST );
                return;
            }

            switch ( sfName )
            {
                case "HCS_ABORT":
                    HandleCmdAbortCancel( recd );
                    break;

                case "HCS_PAUSE":
                    HandleCmdPauseResume( recd );
                    this.SendStateToWeb();
                    break;

                case "HCS_UPDATE":
                    HandleCmdUpdate( recd );
                    break;

                case "HCS_REMOVE":
                    HandleCarrierRemove( recd );
                    break;

            }
        }

        private void MsgS2F49( SFMessage recd )
        {
            Dictionary<string, E_CPACK> cpList = new Dictionary<string, E_CPACK>();

            string cmdID = recd["CommandID"].Value.Trim();
            string carrierId = recd["CarrierID"].Value.Trim();
            string srcUnit = recd["SourcePort"].Value.Trim();
            string dstUnit = recd["DestPort"].Value.Trim();
            int CarrierCleanStatus = recd["CarrierCleanStatus"].IntValue;

            //반송 체크
            bool hasCmd = sql.Command.Has( cmdID );
            if ( hasCmd )
            {
                logger.W( "[SECS] Already exist commandID. id[{0}]", cmdID );
                cpList.Add( "Commnad Exist", E_CPACK.CPNAME_NOT_EXIST );
                ReplyS2F50( recd, E_HCACK.CMD_EXIST, cpList );
                return;
            }

            if ( sql.Command.HasN( x => x.CarrierID.Equals( carrierId ) ) )
            {
                logger.W( "[SECS] Already exist commandID by CarrierID. id[{0}]", carrierId );
                cpList.Add( "Commnad Exist", E_CPACK.CPNAME_NOT_EXIST );
                ReplyS2F50( recd, E_HCACK.CMD_EXIST, cpList );
                return;
            }

            //source dest 체크

            bool hasSrcUnit = sql.Unit.Has( srcUnit );
            if ( !hasSrcUnit )
            {
                logger.W( "[SECS] {0} Source port not exist. ID=[{1}]", cmdID, srcUnit );
                cpList.Add( "SOURCEPORT", E_CPACK.CPNAME_NOT_EXIST ); //6 = Object doesn't exist
            }

            bool hasDst = sql.Unit.Has( dstUnit );
            if ( !hasDst )
            {
                logger.W( "[SECS] {0} Dest port not exist. ID=[{1}]", cmdID, dstUnit );
                cpList.Add( "DESTPORT", E_CPACK.CPNAME_NOT_EXIST );
            }

            bool hasCmdSrc = sql.Command.HasSource( srcUnit );
            if ( hasCmdSrc )
            {
                logger.W( "[SECS] Source port already exist. ID=[{0}]", srcUnit );
                cpList.Add( "SOURCEPORT", E_CPACK.INCORRECT_VAL );
                ReplyS2F50( recd, E_HCACK.NOT_ABLE_TO_EXECUTE, cpList );
                return;
            }

            bool hasCmdDest = sql.Command.HasDest( dstUnit );
            if ( hasCmdDest )
            {
                logger.W( "[SECS] Dest port already exist. ID=[{0}]", dstUnit );
                cpList.Add( "DESTPORT", E_CPACK.INCORRECT_VAL );
                ReplyS2F50( recd, E_HCACK.NOT_ABLE_TO_EXECUTE, cpList );
                return;
            }
            //var fUnit = sql.Unit[srcUnit];
            //var fMachine = sql.Machine[fUnit.MachineID];
            //if(fMachine.Type == TcsMachine.MACHINE_TYPE.TA && fUnit.UnitType == E_UNIT_TYPE.PORT_B2)
            //{
            //    var uB01 = sql.Unit.ListN(x => x.UnitType == E_UNIT_TYPE.PORT_B1).First();
            //    if(uB01.Status == E_UNIT_STATUS.FULL)
            //    {
            //        logger.W("[SECS] B01 Port is Full. ID=[{0}]", srcUnit);
            //        cpList.Add("SOURCEPORT", E_CPACK.INCORRECT_VAL);
            //        ReplyS2F50(recd, E_HCACK.PARAMETER_INVALID, cpList);
            //        return;
            //    }
            //}
            if ( cpList.Count > 0 )
            {
                ReplyS2F50( recd, E_HCACK.PARAMETER_INVALID, cpList );
                return;
            }
            // CURRLOCATION 확인 하는 부분.
            // From Unit = VUITUAL PORT 라도 확인 하도록 변경.
            bool hasCarrier = sql.Carrier.Has( carrierId );
            if ( hasCarrier )
            {
                TcsCarrier carrier = sql.Carrier[carrierId];
                if ( !carrier.CurrUnitID.Equals( srcUnit ) )
                {
                    logger.W( "[SECS] Invalid Carrier CurrLocation ID=[{0}], Loc=[{1}]", carrierId, carrier.CurrUnitID );
                    cpList.Add( "CARRIERID", E_CPACK.CPNAME_NOT_EXIST );
                    ReplyS2F50( recd, E_HCACK.OBJECT_NOT_EXIST, cpList );
                    return;
                }
            }


            bool fromVehicle = sql.Unit[srcUnit].IsVehicle;

            TcsUnit fromUnit = sql.Unit[srcUnit];
            if ( fromVehicle )//Vehicle -> Port
            {
                var vehicle = sql.Unit[srcUnit];

                if ( !vehicle.CanTransfer() )
                {
                    logger.W( "[SECS] Vehicle state or PM state can't transfer, nak 2 reply", vehicle );
                    ReplyS2F50( recd, E_HCACK.NOT_ABLE_TO_EXECUTE, cpList );
                    return;
                }
                if ( !hasCarrier )
                {
                    logger.W( "[SECS] Carrier not exist. ID=[{0}]", carrierId );
                    cpList.Add( "CARRIERID", E_CPACK.CPNAME_NOT_EXIST );
                    ReplyS2F50( recd, E_HCACK.OBJECT_NOT_EXIST, cpList );
                    return;
                }
            }
            else
            {
                if ( !fromUnit.IsVirtual )
                {
                    if ( !hasCarrier )
                    {
                        logger.W( "[SECS] Carrier not exist. ID=[{0}]", carrierId );
                        cpList.Add( "CARRIERID", E_CPACK.CPNAME_NOT_EXIST );
                        ReplyS2F50( recd, E_HCACK.OBJECT_NOT_EXIST, cpList );
                        return;
                    }
                    //2015/1015 source full check
                    if ( fromUnit.Status == E_UNIT_STATUS.EMPTY )
                    {
                        //SOURCE 가 EQP 거나 B04이면 FULL
                        if ( fromUnit.IsVirtual )
                        {
                            //sql.Unit.ChgStatus(fromUnit.ID, E_UNIT_STATUS.FULL);
                        }
                        else
                        {
                            logger.W( "[SECS] Source Unit Staus is EMPTY. ID=[{0}]", fromUnit );
                            cpList.Add( "SOURCEPORT", E_CPACK.INCORRECT_VAL );
                            ReplyS2F50( recd, E_HCACK.NOT_ABLE_TO_EXECUTE, cpList );
                            return;
                        }
                    }
                }

                //from은 Load만 한다.
                if ( !fromUnit.
                    CanUnload )
                {
                    logger.W( "[SECS] FromUnit state or PM state can't transfer, nak 2 reply", fromUnit );
                    ReplyS2F50( recd, E_HCACK.NOT_ABLE_TO_EXECUTE, cpList );
                    return;
                }
            }

            TcsUnit toUnit = sql.Unit[dstUnit];
            if ( !toUnit.CanLoad )
            {
                logger.W( "[SECS] Unit not empty or not normal. ID=[{0}]", dstUnit );
                cpList.Add( "DESTPORT", E_CPACK.INCORRECT_VAL );
                ReplyS2F50( recd, E_HCACK.NOT_ABLE_TO_EXECUTE, cpList );
                return;
            }
            // 선 체크 -------------------------------------------------------------------------

            TcsCarrier ca = new TcsCarrier();
            ca.ID = carrierId;

            TcsCommand cm = new TcsCommand();
            cm.ID = cmdID;
            cm.SrcUnitID = srcUnit;
            cm.DestUnitID = dstUnit;
            cm.Priority = recd["Priority"].IntValue;
            if ( sql.Unit[srcUnit].Status == E_UNIT_STATUS.EMPTY )
            {
                //sql.Unit.ChgStatus(srcUnit, E_UNIT_STATUS.FULL);
                //if (fromUnit.IsVirtual) SendUnitStatusToWeb(srcUnit);
            }
            bool ack = this.HostTransfer( cm, ca );

            ReplyS2F50( recd, ack ? E_HCACK.CONFIRMED : E_HCACK.NOT_ABLE_TO_EXECUTE, cpList );
        }

        private void MsgS5F5( SFMessage recd )
        {
            SFMessage rep = mgr["LAD"];
            rep.Systembyte = recd.Systembyte;

            var reqAlarm = sql.AlarmDef.All;
            if ( recd["ALIDCOUNT"].Children.Count != 0 ) reqAlarm = reqAlarm.FwContains( x => x.ID, recd["ALIDCOUNT"].Children.Select( t => t.IntValue ) ).ToList();

            rep["ALIDCOUNT"].DuplicateChildren( reqAlarm.Count );
            for ( int i = 0; i < reqAlarm.Count; i++ )
            {
                rep["ALCD" + i].IntValue = reqAlarm[i].Code;
                rep["ALID" + i].IntValue = reqAlarm[i].ID;
                rep["ALTX" + i].Value = reqAlarm[i].TextTA;
            }

            ReplyHsms( rep );
        }
        // -----------------//
        private void MsgS1F1( SFMessage recd )
        {
            SFMessage rep = mgr["D_Eqp"];
            rep.Systembyte = recd.Systembyte;
            ReplyHsms( rep );
        }

        private void MsgS1F13( SFMessage recd )
        {
            SFMessage rep = mgr["CRA_RCP"];
            rep["MDLN"].Value = sql.HsmsConfig.EqpName.Value; //MDLN = Model Name of the equipment 
            rep["SOFTREV"].Value = this.VER_SECS.Substring( 0, 6 );//substring 0,6
            rep.Systembyte = recd.Systembyte;
            ReplyHsms( rep );
        }

        private void MsgS1F15( SFMessage recd )
        {
            int mode = GetControlMode();

            SFMessage rep = mgr["OFLA"];
            rep.Systembyte = recd.Systembyte;

            if ( mode <= 3 )
            {
                rep["OFLACK"].IntValue = 2;
                ReplyHsms( rep );
                EventRpt1( H_CEID.OFFLINE_1 );
            }
            else
            {
                rep["OFLACK"].IntValue = 0;
                ReplyHsms( rep );
                //send to msg - kbs 06/14
                EventRpt1( H_CEID.OFFLINE_1 );
                sql.HsmsConfig.ChgControlState( E_CTL_STATE.PAUSED );
                sql.HsmsConfig.ChgControlMode( E_CONTROLSTATE.HOST_OFFLINE );

                SendStateToWeb();
            }
        }

        private void MsgS1F17( SFMessage recd )
        {
            int mode = GetControlMode();

            SFMessage rep = mgr["ONLA"];
            rep.Systembyte = recd.Systembyte;

            if ( mode >= 4 )
            {
                rep["ONLACK"].IntValue = EnumUtils.IntV( E_ONLACK.ALREADY_ONLINE );
                ReplyHsms( rep );
            }
            else
            {
                rep["ONLACK"].IntValue = EnumUtils.IntV( E_ONLACK.CONFIRMED );
                ReplyHsms( rep );
                sql.HsmsConfig.ChgControlMode( E_CONTROLSTATE.ONLINE_REMOTE );
                SendStateToWeb();
            }
        }

        void mgr_OnConnect( string id )
        {
            logger.I( "[SECS] Hsms Connected" );
            this.sql.HsmsConfig.ChgConnectState( E_CONN_STATE.CONNECT );
            this.sql.HsmsConfig.ChgControlMode( E_CONTROLSTATE.ONLINE_REMOTE );
            SendStateToWeb();
        }

        void mgr_OnDisconnect( string id, Exception e )
        {
            logger.I( "[SECS] Hsms Disconnected !!" );
            this.sql.HsmsConfig.ChgConnectState( E_CONN_STATE.DIS_CONNECT );
            this.sql.HsmsConfig.ChgControlMode( E_CONTROLSTATE.HOST_OFFLINE );
            SendStateToWeb();
        }

        void mgr_OnReceive( SFMessage recd )
        {
            ThreadUtils.InvokePool( PoolSecsRecd, recd );
        }

        void mgr_OnSendComplete( SFMessage msg )
        {
            logger.I( string.Format( "{0} : {1}", "[TCS->MCS]", msg.LogFormat() ) );
            //logger.I(msg.LogFormat());
        }

        void mgr_OnLog( string id, string log )
        {
            logger.I( "[SECS] " + log );
        }

        void mgr_OnTimeout( SFMessage msg )
        {
            logger.W( msg.LogFormat() );
        }

        void ReplyHsms( SFMessage rep )
        {
            mgr.Send( rep );
        }

        void RequestHsms( SFMessage req )
        {
            if ( GetControlMode() <= 3 )
            {
                logger.W( "[SECS] SECS Message Skip. Control Mode is Offline. MSG : {0}", req.SxFx );
                return;
            }
            mgr.Send( req );
        }

        private void HandleS1F3Init( SFMessage recd )
        {
            SFMessage rep = mgr["SSD-Init"];
            rep.Systembyte = recd.Systembyte;

            //VID 6 -------------------------------------------------------

            try
            {
                rep["ControlState"].IntValue = sql.HsmsConfig.ControlMode.ConnectIntValue;
            }
            catch ( Exception ex )
            {
                logger.I( "[SECS] " + ex.ToString() );
            }
            //enhanced carrier
            //VID 21 ------------------------------------------------------
            {
                var cars = sql.Carrier.All;
                var ll = cars.Where( x => sql.Unit.Has( x.CurrUnitID ) ).ToList();

                rep["EnhancedCarriers"].DuplicateChildren( ll.Count );
                for ( int i = 0; i < ll.Count; i++ )
                {
                    TcsCarrier car = ll[i];//sql.Carrier.Get(carID);

                    string vehid = string.Empty;

                    if ( sql.Unit.Has( car.CurrUnitID ) )
                        vehid = car.CurrUnitID;

                    rep["CarrierID" + i].Value = car.ID;
                    rep["VehicleID" + i].Value = vehid;
                    rep["CarrierLoc" + i].Value = car.CurrUnitID;
                    rep["InstallTime" + i].Value = car.AddTime.ToString( "yyyyMMddHHmmssff" );
                    rep["CarrierCleanStatus" + i].IntValue = EnumUtils.IntV( car.CleanState );
                }
            }
            //VID 23 ------------------------------------------------------
            {
                var cmds = sql.Command.All
                    .Where( x => x.IsCycle == false )
                    .ToList()
                    ;
                rep["EnhancedTransfers"].DuplicateChildren( cmds.Count );

                for ( int i = 0; i < cmds.Count; i++ )
                {
                    var cmd = cmds[i];
                    TcsCarrier car = cmd.Carrier;

                    rep["CommandID" + i].Value = cmds[i].ID;
                    rep["Priority" + i].IntValue = cmds[i].Priority;

                    //Transfer state
                    //-. 1 = queued
                    //-. 2 = transferring
                    //-. 3 = paused
                    //-. 4 = canceling
                    //-. 5 = aborting
                    //-. 6 = waiting
                    int trfState = cmds[i].State == E_CMD_STATE.NEW ? 1 : 2;

                    rep["TransferState" + i].IntValue = trfState;
                    rep["CarrierID" + i].Value = cmds[i].CarrierID;
                    rep["SourcePort" + i].Value = cmds[i].SrcUnitID;
                    rep["DestPort" + i].Value = cmds[i].DestUnitID;
                    rep["CarrierCleanStatus" + i].IntValue = EnumUtils.IntV( car.CleanState );
                }
            }
            //VID 25 ------------------------------------------------------
            {
                IList<TcsUnit> vcs = sql.Unit.AllVehicle();
                rep["EnhancedVehicles"].DuplicateChildren( vcs.Count );

                for ( int i = 0; i < vcs.Count; i++ )
                {
                    rep["VehicleID" + i].Value = vcs[i].ID;
                    rep["VehicleState" + i].IntValue = vcs[i].GetVehicleState();
                    rep["VehicleStatus" + i].IntValue = vcs[i].V_Status();
                }
            }
            //VID 25 -----------------------------------------------------
            {
                IList<TcsAlarm> ams = sql.Alarm.All;
                rep["EnabledUnitAlarms"].DuplicateChildren( ams.Count );

                for ( int i = 0; i < ams.Count; i++ )
                {
                    rep["UnitID" + i].Value = ams[i].UnitID;
                    rep["AlarmID" + i].IntValue = ams[i].AlarmNo;
                    rep["AlarmText" + i].Value = ams[i].AlarmText;
                }
            }
            //VID 25 -----------------------------------------------------
            {
                IList<TcsUnit> unit = sql.Unit.All.Where( x => !x.IsVehicle ).ToList();
                rep["EnhancedUnitInfos"].DuplicateChildren( unit.Count );

                for ( int i = 0; i < unit.Count; i++ )
                {
                    rep["UnitName" + i].Value = unit[i].ID;
                    rep["UnitState" + i].IntValue = unit[i].Port_State();
                }
            }
            ReplyHsms( rep );
            SendStateToWeb();
        }
    }
}