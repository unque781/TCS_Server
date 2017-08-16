using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Floware.Concurrent;
using Floware.MQ;
using Floware.Utils;
using Floware.LINQ;
using TcsV2.Core;
using TcsV2.Core.DTO;
using TcsV2.Core.MODEL;


namespace TCS.ServerV2
{

    partial class FormMain
    {
        private MQClient mq = new MQClient();

        #region MQ 관련.
        void InitMQ()
        {
            try
            {
                mq.Config.IpAddress = sql.AppConfig.FmqIPAddress.Value;
                mq.Config.Port = sql.AppConfig.FmqPort.IntValue;
                mq.Config.MyName = sql.AppConfig.FmqServerSubject.Value;
                mq.Config.ToName = sql.AppConfig.FmqWebSubject.Value;
                mq.OnMQRecd += (_OnMQRecd);
                mq.OnMQDiscontd += (_OnMQDiscontd);
                mq.OnMQSent += (_OnMQSent);
                mq.OnMQContd += (_OnMQContd);
                mq.OnMQActiveChgd += (_OnMQActiveChgd);
                mq.Config.RcvMode = MQConfig.Mode.Queue;
                mq.Init();

            }
            catch (Exception e)
            {
                logger.E("[FMQ] " + e.Message + " [" + e.StackTrace + "]");
            }
        }

        void _OnMQActiveChgd(string id, bool isActive)
        {
            if (SERVER_ACTIVE == isActive) return;

            SERVER_ACTIVE = isActive;

            try
            {
                DlgUtils.Invoke(DspFmqState);

                //초기 Start 된경우는 무시한다.
                if (INITSTART)
                {
                    INITSTART = false;
                    return;
                }

                if (isActive)
                {
                    if (!this.mgr.Connected)
                        this.mgr.Connect(false, 10000);

                    this.InitQuartz();
                }
                else
                {
                    this.StopQuartz();
                    this.mgr.Disconnect();
                }
            }
            catch (Exception e)
            {
                logger.E("[FMQ] " + e.Message + " [" + e.StackTrace + "]");
            }
        }

        void _OnMQSent(MQMessage msg) { }

        void _OnMQContd(MQConfig config)
        {
            logger.I("[FMQ] Connect Info={0}", config);
            this.tssFMQConnection.Image = Properties.Resources.Start;
        }

        void _OnMQDiscontd(MQConfig config)
        {
            logger.I("[FMQ] Disconnect Info={0}", config);
            this.tssFMQConnection.Image = Properties.Resources.Stop;
        }

        void _OnMQRecd(MQMessage mqmsg)
        {
            string command = mqmsg.GetFieldV("COMMAND");

            //ThreadUtils.InvokePool(PoolMQRecd, mqmsg, command);
            PoolMQRecd(mqmsg, command);
        }

        void PoolMQRecd(MQMessage orgMsg, string command)
        {
            this.ScreenLog(string.Format("Web Message={0}", orgMsg.GetValue().ToString()));

            try
            {
                switch (command)
                {
                    case "TRANSFERCANCEL":
                        HandleWebCancel(orgMsg);
                        break;
                    case "COMMANDDEL":
                        HandleWebCommandDel(orgMsg);
                        break;
                    case "PMACTION":
                        HandleWebPM(orgMsg);
                        break;
                    case "TRAVELRESET":
                        HandleWebTravelReset(orgMsg);
                        break;
                    case "TRANSFER":
                        HandleWebCommand(orgMsg);
                        break;
                    case "CARRIERHANDEL":
                        HandleWebCarrier(orgMsg);
                        break;
                    case "VEHICLEINSTALL":
                        HandleWebVehicleInstall(orgMsg);
                        break;
                    case "QUARTZCHANGE":
                        HandleWebQuartzChange(orgMsg);
                        break;
                    case "ALARM":
                        AlarmReport(orgMsg);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception e)
            {
                logger.E("[FMQ] " + e.Message + " [" + e.StackTrace + "]");
            }
        }
        #endregion

        #region HostCommand 관련.

        //Host 목적지 변경 처리.
        private int HostDestChange(TcsCommand newCmd)
        {
            TcsCommand oldCmd = this.sql.Command[newCmd.ID];

            TcsCarrier carrier = this.sql.Carrier[oldCmd.CarrierID];

            string currPoint = string.Empty;
            bool isSocUnit = sql.Unit.Has(carrier.CurrUnitID);
            if (!isSocUnit)
            {
                return 2;
            }
            else
            {
                TcsUnit socUnit = sql.Unit[carrier.CurrUnitID];
                currPoint = socUnit.StationID.ToString();
            }

            if (!sql.Unit.Has(newCmd.DestUnitID))
            {
                return 2;
            }

            TcsUnit detUnit = sql.Unit[newCmd.DestUnitID];

            if (oldCmd.State == E_CMD_STATE.NEW)
            {
                this.sql.Command.ChgDest(oldCmd.ID, detUnit.ID, detUnit.StationID.ToString(), newCmd.FinalLocation);
                return 0;
            }

            TcsCommandChg chg = new TcsCommandChg
            {
                ID = oldCmd.ID,
                CarrierID = oldCmd.CarrierID,
                SrcUnitID = carrier.CurrUnitID,
                SrcPointID = currPoint,
                DestUnitID = detUnit.ID,
                DestPointID = detUnit.StationID.ToString(),
                FinalLoc = newCmd.FinalLocation,
                VehicleID = oldCmd.VehicleID
            };

            this.sql.CommandChg.AddN(chg);

            TcsMachine m = sql.Machine[oldCmd.MachineID];
            //Dest change 조건 변경.
            bool isOk = sql.Command.Has(oldCmd.ID);
            if (isOk)
            {
                this.sql.Command.ChgDest(chg.ID, chg.DestUnitID, chg.DestPointID, chg.FinalLoc);
                return 0;
            }
            else
            {
                this.sql.CommandChg.Delete(chg.ID);
                return 2;
            }
        }

        bool HostTransfer(TcsCommand hcmd, TcsCarrier hcar)
        {
            hcar.CurrUnitID = hcmd.SrcUnitID;

            TcsCommand cmd = hcmd;
            cmd.Carrier = hcar;
            cmd.CarrierID = hcar.ID;

            MQMessage mq = null;
            try
            {
                //this.sql.Carrier.AddN( hcar );
                return this.ExecuteCommand(cmd, out mq);
            }
            catch (Exception e)
            {

                logger.E("[FMQ] " + e.Message + " [" + e.StackTrace + "]");
                return false;
            }
        }

        void HostAbort(TcsCommand cmd, bool isTcs, string userID, string ipAddr)
        {
            if (!this.sql.Command.Has(cmd.ID))
            {
                string text = string.Format("Command={0} Delete Fail - Cmd Source[{1}] / Cmd Dest[{2}]", cmd.ID, cmd.SrcUnitID, cmd.DestUnitID);    //170726 Kkm 자세한 LOG 확인을 위해 Source, Dest Port 추가
                this.sql.HisUserAction.AddHisAction(userID, ipAddr, TcsUserHisAction.ActionMenu.COMMAND_DELETE, TcsUserHisAction.ActionType.DELETE, text, "NG");
                return;
            }

            TcsCommand c = this.sql.Command[cmd.ID];

            E_BYWHO byWho = isTcs ? E_BYWHO.TCS : E_BYWHO.HOST;
            this.sql.Command.ChgByWho(c.ID, byWho);
            //Manual Abort 일때만 OperatorInitAction 처리함.
            if (isTcs && !userID.Equals("TCS"))
            {
                this.EventRpt9(H_CEID.OPERATOR_INIT_ACTION_501, c, H_OPER_INIT.ABORT);
            }

            if (!c.IsAbortState)
            {
                if (isTcs)
                {
                    string text = string.Format("Command={0} Delete Fail - Cmd Source[{1}] / Cmd Dest[{2}]", c.ID, c.SrcUnitID, c.DestUnitID);  //170726 Kkm 자세한 LOG 확인을 위해 Source, Dest Port 추가
                    this.sql.HisUserAction.AddHisAction(userID, ipAddr, TcsUserHisAction.ActionMenu.COMMAND_DELETE, TcsUserHisAction.ActionType.DELETE, text, "NG");
                }
                //Abort Init
                EventRpt3(H_CEID.TRANSFER_ABORT_INIT_203, c);
                //Abort Fail
                EventRpt3(H_CEID.TRANSFER_ABORT_FAIL_202, c);
                return;
            }
            H_HOST_STATE hostState = isTcs ? H_HOST_STATE.MNL_ABORT : H_HOST_STATE.HOST_ABORT;
            this.sql.Command.ChgHostState(c.ID, hostState);

            //State변경
            this.sql.Command.ChgState(c.ID, E_CMD_STATE.ABORT);

            //History Add
            this.sql.HisCommand.AddHis(this.sql.Command[c.ID]);

            this.EventRpt3(H_CEID.TRANSFER_ABORT_INIT_203, c);
            if (c.Assigned)
            {
                this.sql.Unit.ChgSubState(c.VehicleID, E_VEHICLE_SUB_STATE.NOTASSIGN);
                this.SendVehicleSubStateToWeb(c.VehicleID); // State / SubState order change
                SendVehicleStatusToWeb(c.VehicleID);
                this.EventRpt6(H_CEID.VEHICLE_UNASSIGN_610, c, c.SrcUnitID);
            }
            LockUtils.Wait(1000);
            EventRpt3(H_CEID.TRANSFER_ABORT_COMP_201, c);
            sql.Carrier.ChgCommandID(cmd.CarrierID, "");
            this.sql.Command.Delete(c.ID);

            DeleteVirtualPortData( c.Carrier );
            //2017/04/02 by MC
            //cmd delete 시 carrier current Unit = virtual 이면 carrier 함께 삭제.
            //if ( this.sql.Carrier.Has( c.CarrierID ) )
            //{
            //    TcsCarrier carrier = sql.Carrier[c.CarrierID];
            //    if ( carrier.CurrUnit.IsVirtual )
            //        sql.Carrier.Del( carrier.ID );
            //    else
            //        sql.Carrier.ChgCommandID( carrier.ID, "" );
            //}

            string content = string.Format("Command={0} Delete Success - Cmd Source[{1}] / Cmd Dest[{2}]", c.ID, cmd.SrcUnitID, cmd.DestUnitID);    //170726 Kkm 자세한 LOG 확인을 위해 Source, Dest Port 추가
            if (isTcs)
                this.sql.HisUserAction.AddHisAction(userID, ipAddr, TcsUserHisAction.ActionMenu.COMMAND_DELETE, TcsUserHisAction.ActionType.DELETE, content, "OK");

            this.OnMoveReqAbort(c.VehicleID);
        }
        void TcsCancel(TcsCommand hcmd, bool isTcs, string userID)
        {
            if (!this.sql.Command.Has(hcmd.ID))
                return;

            TcsCommand c = this.sql.Command[hcmd.ID];

            E_BYWHO byWho = isTcs ? E_BYWHO.TCS : E_BYWHO.HOST;
            this.sql.Command.ChgByWho(c.ID, byWho);

            this.sql.Command.ChgHostState(c.ID, H_HOST_STATE.MNL_CANCEL);

            //State변경
            this.sql.Command.ChgState(c.ID, E_CMD_STATE.CANCEL);

            //History Add
            this.sql.HisCommand.AddHis(this.sql.Command[c.ID]);

            this.EventRpt3(H_CEID.TRANSFER_CANCEL_INIT_206, c);

            if (c.Assigned)
            {
                this.sql.Unit.ChgSubState(c.VehicleID, E_VEHICLE_SUB_STATE.NOTASSIGN);
                SendVehicleStatusToWeb(c.VehicleID);
                this.SendVehicleSubStateToWeb(c.VehicleID);
                this.EventRpt6(H_CEID.VEHICLE_UNASSIGN_610, c, c.SrcUnitID);
            }

            LockUtils.Wait(1000);
            EventRpt3(H_CEID.TRANSFER_CANCEL_COMP_204, c);

            sql.Carrier.ChgCommandID( c.CarrierID, "" );
            this.sql.Command.Delete(c.ID);
            this.DeleteVirtualPortData( c.Carrier );
            //2017/04/02 by MC
            //cmd delete 시 carrier current Unit = virtual 이면 carrier 함께 삭제.
            //if ( this.sql.Carrier.Has( c.CarrierID ) )
            //{
            //    TcsCarrier carrier = sql.Carrier[c.CarrierID];
            //    if ( carrier.CurrUnit.IsVirtual )
            //        sql.Carrier.Del( carrier.ID );
            //    else
            //        sql.Carrier.ChgCommandID( c.CarrierID, "" );
            //}

            string content = string.Format("Command={0} Delete Success - Cmd Source[{1}] / Cmd Dest[{2}]", c.ID, c.SrcUnitID, c.DestUnitID);    //170726 Kkm 자세한 LOG 확인을 위해 Source, Dest Port 추가

            if (c.IsCancelState)
            {
                if (c.State != E_CMD_STATE.NEW)
                    OnMoveReqAbort(c.VehicleID);
            }
        }
        void HostCancel(TcsCommand hcmd, bool isTcs, string userID, string ipAddr)
        {
            if (!this.sql.Command.Has(hcmd.ID))
            {
                string text = string.Format("Command={0} Delete Fail - Cmd Source[{1}] / Cmd Dest[{2}]", hcmd.ID, hcmd.SrcUnitID, hcmd.DestUnitID); //170726 Kkm 자세한 LOG 확인을 위해 Source, Dest Port 추가
                this.sql.HisUserAction.AddHisAction(userID, ipAddr, TcsUserHisAction.ActionMenu.COMMAND_DELETE, TcsUserHisAction.ActionType.DELETE, text, "NG");
                return;
            }

            TcsCommand c = this.sql.Command[hcmd.ID];

            E_BYWHO byWho = isTcs ? E_BYWHO.TCS : E_BYWHO.HOST;
            this.sql.Command.ChgByWho(c.ID, byWho);

            //Manual Cancel 일때만 OperatorInitAction 처리함.
            if (isTcs && !userID.Equals("TCS"))
            {
                EventRpt9(H_CEID.OPERATOR_INIT_ACTION_501, c, H_OPER_INIT.CANCEL);
            }

            if (!c.IsCancelState)
            {
                if (isTcs)
                {
                    string text = string.Format("Command={0} Delete Fail - Cmd Source[{1}] / Cmd Dest[{2}]", c.ID, hcmd.SrcUnitID, hcmd.DestUnitID);    //170726 Kkm 자세한 LOG 확인을 위해 Source, Dest Port 추가
                    this.sql.HisUserAction.AddHisAction(userID, ipAddr, TcsUserHisAction.ActionMenu.COMMAND_DELETE, TcsUserHisAction.ActionType.DELETE, text, "NG");
                }
                this.EventRpt3(H_CEID.TRANSFER_CANCEL_INIT_206, c);
                LockUtils.Wait(1000);
                this.EventRpt3(H_CEID.TRANSFER_CANCEL_FAIL_205, c);

                this.sql.Unit.ChgSubState(c.VehicleID, E_VEHICLE_SUB_STATE.NOTASSIGN);
                this.SendVehicleSubStateToWeb(c.VehicleID); // State / SubState order change
                SendVehicleStatusToWeb(c.VehicleID);

                return;
            }

            //NEW 상태임.
            //HOST 상태변경
            H_HOST_STATE hostState = isTcs ? H_HOST_STATE.MNL_CANCEL : H_HOST_STATE.HOST_CANCEL;
            this.sql.Command.ChgHostState(c.ID, hostState);

            //State변경
            this.sql.Command.ChgState(c.ID, E_CMD_STATE.CANCEL);

            //History Add
            this.sql.HisCommand.AddHis(this.sql.Command[c.ID]);

            this.EventRpt3(H_CEID.TRANSFER_CANCEL_INIT_206, c);

            if (c.Assigned)
            {
                this.sql.Unit.ChgSubState(c.VehicleID, E_VEHICLE_SUB_STATE.NOTASSIGN);
                this.SendVehicleSubStateToWeb(c.VehicleID); // State / SubState order change
                SendVehicleStatusToWeb(c.VehicleID);
                this.EventRpt6(H_CEID.VEHICLE_UNASSIGN_610, c, c.SrcUnitID);
            }
            LockUtils.Wait(1000);
            EventRpt3(H_CEID.TRANSFER_CANCEL_COMP_204, c);
            this.sql.Command.Delete(c.ID);

            //2017/04/02 by MC
            //cmd delete 시 carrier current Unit = virtual 이면 carrier 함께 삭제.
            if (this.sql.Carrier.Has(c.CarrierID))
            {
                TcsCarrier carrier = sql.Carrier[c.CarrierID];
                if (carrier.CurrUnit.IsVirtual)
                    sql.Carrier.Del(carrier.ID);
                else
                    sql.Carrier.ChgCommandID(carrier.ID, "");
            }

            string content = string.Format("Command={0} Delete Success - Cmd Source[{1}] / Cmd Dest[{2}]", c.ID, hcmd.SrcUnitID, hcmd.DestUnitID);      //170726 Kkm 자세한 LOG 확인을 위해 Source, Dest Port 추가
            if (isTcs) // Web에서 Cancel 할경우, Operatorinit
            {
                this.sql.HisUserAction.AddHisAction(userID, ipAddr, TcsUserHisAction.ActionMenu.COMMAND_DELETE, TcsUserHisAction.ActionType.DELETE, content, "OK");
            }

            if (c.State != E_CMD_STATE.NEW) OnMoveReqAbort(c.VehicleID);
        }

        void HandleWebCommand(MQMessage mqmsg)
        {
            string[] tmpMsg = mqmsg.GetFieldV("MSG").Split(' ');

            this.ScreenLog(string.Format("Web Message={0}", mqmsg.GetValue().ToString()));

            WebCommandDto dto = new WebCommandDto
            {
                CarrierID = tmpMsg[0],
                SrcUnit = tmpMsg[1],
                DestUnit = tmpMsg[2],
                UserID = tmpMsg[3],
                IPAddr = tmpMsg[4],
            };

            //TcsCommand cmd = dto.TcsCommand;

            TcsCommand cmd = new TcsCommand
            {
                CreateUserID = dto.UserID,
                SrcUnitID = dto.SrcUnit,
                DestUnitID = dto.DestUnit,
                CarrierID = dto.CarrierID,
                NakCode = 0,
            };

            if (cmd.CreateUserID == string.Empty) cmd.CreateUserID = dto.UserID;
            cmd.FinalLocation = cmd.DestUnitID;

            bool isCommand = this.sql.Command.HasByCarrierID(cmd.CarrierID);
            if (isCommand)
            {
                cmd.NakCode = 2;
                this.ReplyToWeb(cmd.NakCode, mqmsg);
                return;
            }

            //단위반송
            cmd.Carrier = new TcsCarrier
            {
                ID = cmd.CarrierID,
                CommandID = cmd.ID,
                CurrUnitID = cmd.SrcUnitID,
            };
            MQMessage mqReply = null;
            bool isCreate = ExecuteCommand(cmd, out mqReply);
            //cmd.NakCode = isCreate ? 0 : 2;            
            mqReply.AddField("NakCode", isCreate ? 0 : 2);

            if (isCreate)
            {
                TcsCommand ncmd = this.sql.Command.All.Where(x => x.CarrierID == cmd.CarrierID).First();
                this.EventRpt9(H_CEID.OPERATOR_INIT_ACTION_501, ncmd, H_OPER_INIT.TRANSFER);

                string content = string.Format("Carrier={0} Command Create - Cmd Source[{1}] / Cmd Dest[{2}]", ncmd.CarrierID, ncmd.SrcUnitID, ncmd.DestUnitID);        //170726 Kkm 자세한 LOG 확인을 위해 Source, Dest Port 추가
                this.sql.HisUserAction.AddHisAction(dto, TcsUserHisAction.ActionMenu.COMMAND, TcsUserHisAction.ActionType.ADD, content, "OK");
            }
            else
            {
                this.sql.Command.DeleteByCarrierID(cmd.CarrierID);
                this.DeleteVirtualPortData( cmd.Carrier );

                //string content = string.Format("Carrier={0} Command Create Fail", cmd.CarrierID);
                string content = mqReply.GetFieldV("MSG");
                this.sql.HisUserAction.AddHisAction(dto, TcsUserHisAction.ActionMenu.COMMAND, TcsUserHisAction.ActionType.ADD, content, "NG");
            }

            if (mqmsg.IsRequest)
            {
                this.ReplyToWeb(mqReply, mqmsg);
                //this.ReplyToWeb(cmd.NakCode, mqmsg );
                return;
            }
        }

        /// <summary>
        /// CommandID
        /// </summary>
        private void HandleWebCancel(MQMessage mqmsg)
        {
            //tmpMsg[0] : commandID
            //tmpMsg[1] : UserID
            //tmpMsg[2] : ipAddr
            string[] tmpMsg = mqmsg.GetFieldV("MSG").Split(' ');

            this.ScreenLog(string.Format("Web Message={0}", mqmsg.GetValue().ToString()));

            //string msg = mqmsg.GetFieldV("MSG").Trim();

            WebTransferCancelDto dto = new WebTransferCancelDto();
            dto.CommandID = tmpMsg[0];
            dto.UserID = tmpMsg[1];
            dto.IPAddr = tmpMsg[2];
            dto.NakCode = 0;

            TcsCommand c = new TcsCommand();
            MQMessage mqReply = new MQMessage();

            bool isCmd = this.sql.Command.Has(dto.CommandID);
            if (!isCmd)
            {
                dto.NakCode = 2;
                logger.I("[FMQ] CommandID={0} not exist", dto.CommandID);

                mqReply.AddField("NakCode", 2);
                mqReply.AddField("MSG", string.Format("CommandID={0} not exist, Delete Fail", dto.CommandID));

                this.ReplyToWeb(mqReply, mqmsg);

                string content = string.Format("Command={0} Delete Fail - Cmd Source[{1}] / Cmd Dest[{2}]", dto.CommandID, c.SrcUnitID, c.DestUnitID);          //170726 Kkm 자세한 LOG 확인을 위해 Source, Dest Port 추가
                this.sql.HisUserAction.AddHisAction(dto, TcsUserHisAction.ActionMenu.COMMAND_DELETE, TcsUserHisAction.ActionType.DELETE, content, "NG");

                return;
            }

            var cmd = this.sql.Command[dto.CommandID];

            if (cmd.IsCancelState)
            {
                this.HostCancel(cmd, true, dto.UserID, dto.IPAddr);
                this.ReplyToWeb(dto.NakCode, mqmsg);
            }
            else if (cmd.IsAbortState)
            {
                this.HostAbort(cmd, true, dto.UserID, dto.IPAddr);
                this.ReplyToWeb(dto.NakCode, mqmsg);
            }
            else
            {
                dto.NakCode = 2;
                logger.I("[FMQ] CommandID={0} CarrierID={1} Current State={2} Cancel or Abort not allow", cmd.ID, cmd.Carrier, cmd.State);

                mqReply.AddField("NakCode", 2);
                mqReply.AddField("MSG", string.Format("CommandID={0} CarrierID={1} Current State={2} Cancel or Abort not allow", cmd.ID, cmd.CarrierID, cmd.State));

                this.ReplyToWeb(mqReply, mqmsg);

                string content = string.Format("Command={0} Delete Fail - Cmd Source[{1}] / Cmd Dest[{2}]", dto.CommandID, c.SrcUnitID, c.DestUnitID);      //170726 Kkm 자세한 LOG 확인을 위해 Source, Dest Port 추가
                this.sql.HisUserAction.AddHisAction(dto, TcsUserHisAction.ActionMenu.COMMAND_DELETE, TcsUserHisAction.ActionType.DELETE, content, "NG");

                return;
            }
        }

        private void HandleWebCommandDel(MQMessage mqmsg)
        {
            string[] tmpMsg = mqmsg.GetFieldV("MSG").Split(' ');

            WebCommandDelDto dtoWeb = new WebCommandDelDto
            {
                CommandID = tmpMsg[0],
                UserID = tmpMsg[1],
                IPAddr = tmpMsg[2],
                NakCode = 0,
            };

            try
            {
                {
                    if (!this.sql.Command.Has(dtoWeb.CommandID))
                    {
                        if (mqmsg.IsRequest)
                        {
                            dtoWeb.NakCode = 2;
                            this.ReplyToWeb(dtoWeb.NakCode, mqmsg);
                            return;
                        }
                    }

                    TcsCommand cmd = this.sql.Command[dtoWeb.CommandID];
                    if (!cmd.IsStateNew)
                    {
                        //State 변경.
                        this.sql.Command.ChgState(cmd.ID, E_CMD_STATE.ABORT);

                        //Host State 변경.
                        this.sql.Command.ChgHostState(cmd.ID, H_HOST_STATE.MNL_ABORT);

                        string content = string.Format("Command={0} WebDelete Pass - Cmd Source[{1}] / Cmd Dest[{2}]", cmd.ID, cmd.SrcUnitID, cmd.DestUnitID);          //170726 Kkm 자세한 LOG 확인을 위해 Source, Dest Port 추가
                        this.sql.HisUserAction.AddHisAction(dtoWeb.UserID, dtoWeb.IPAddr, TcsUserHisAction.ActionMenu.COMMAND_ABORT, TcsUserHisAction.ActionType.DELETE, content, "OK");

                        //Abort Init
                        this.EventRpt3(H_CEID.TRANSFER_ABORT_INIT_203, cmd);

                        LockUtils.Wait(100);

                        //Abort Complete
                        this.EventRpt3(H_CEID.TRANSFER_ABORT_COMP_201, cmd);
                    }
                    //NEW 상태인 자재는 Cancel 처리한다.
                    else
                    {
                        //State 변경.
                        this.sql.Command.ChgState(cmd.ID, E_CMD_STATE.CANCEL);

                        //Host State 변경.
                        this.sql.Command.ChgHostState(cmd.ID, H_HOST_STATE.MNL_CANCEL);

                        string content = string.Format("Command={0} WebDelete Pass - Cmd Source[{1}] / Cmd Dest[{2}]", cmd.ID, cmd.SrcUnitID, cmd.DestUnitID);          //170726 Kkm 자세한 LOG 확인을 위해 Source, Dest Port 추가
                        this.sql.HisUserAction.AddHisAction(dtoWeb.UserID, dtoWeb.IPAddr, TcsUserHisAction.ActionMenu.COMMAND_DELETE, TcsUserHisAction.ActionType.DELETE, content, "OK");

                        //CancelInit
                        this.EventRpt3(H_CEID.TRANSFER_CANCEL_INIT_206, cmd);

                        LockUtils.Wait(100);

                        //CencelComplete
                        this.EventRpt3(H_CEID.TRANSFER_CANCEL_COMP_204, cmd);
                    }

                    //History 추가.
                    this.sql.HisCommand.AddHis(cmd);
                    LockUtils.Wait(100);
                    this.sql.Command.Delete(cmd.ID);
                    //2017/04/02 by MC
                    //cmd delete 시 carrier current Unit = virtual 이면 carrier 함께 삭제.
                    if (sql.Carrier.Has(cmd.CarrierID))
                    {
                        TcsCarrier carrier = sql.Carrier[cmd.CarrierID];
                        if (carrier.CurrUnit.IsVirtual)
                        {
                            sql.Carrier.Del(carrier.ID);
                        }
                        else
                        {
                            sql.Carrier.ChgCommandID(cmd.CarrierID, "");
                        }
                    }
                }

                if (mqmsg.IsRequest)
                {
                    dtoWeb.NakCode = 0;
                    this.ReplyToWeb(dtoWeb.NakCode, mqmsg);
                }
            }
            catch (Exception e)
            {
                logger.E("[FMQ] " + e.Message + " [" + e.StackTrace + "]");

                if (mqmsg.IsRequest)
                {
                    dtoWeb.NakCode = 2;
                    this.ReplyToWeb(dtoWeb.NakCode, mqmsg);
                }
            }
        }

        void HandleWebCarrier(MQMessage mqmsg)
        {
            string[] tmpMsg = mqmsg.GetFieldV("MSG").Split(' ');

            this.ScreenLog(string.Format("Web Message={0}", mqmsg.GetValue().ToString()));

            WebCarrierDto webCar = new WebCarrierDto
            {
                CarrierID = tmpMsg[0],
                UnitID = tmpMsg[1],
                ActionType = (WebCarrierDto.Action)Enum.Parse(typeof(WebCarrierDto.Action), tmpMsg[2]),
                UserID = tmpMsg[3],
                IPAddr = tmpMsg[4],
                NakCode = 0,
            };

            MQMessage mqReply = new MQMessage();
            try
            {
                bool isRun = this.sql.Command.HasByCarrierID(webCar.CarrierID);
                Assert.IsFalse(isRun, "Carrier = {0} Is Transfer", webCar.CarrierID);

                TcsUnit u = sql.Unit[webCar.UnitID];

                switch (u.UnitType)
                {
                    case E_UNIT_TYPE.NONE:
                        break;
                    case E_UNIT_TYPE.PORT_B1:
                        break;
                    case E_UNIT_TYPE.PORT_B2:
                        break;
                    case E_UNIT_TYPE.PORT_MCT:
                        break;
                    case E_UNIT_TYPE.PORT_EQP:
                        break;
                    case E_UNIT_TYPE.PORT_OHS:
                        break;
                    case E_UNIT_TYPE.FTE:
                    case E_UNIT_TYPE.MCT:
                        {
                            Assert.NotNull(u, "CIMCARRIER Install CarrerID={0} not exist", webCar.CarrierID);
                            Assert.NotNull(u, "CIMCARRIER Install UnitID={0} not exist", webCar.UnitID);

                            plc.WriteWord(u.ID + SPLIT_PREFIX + E_PLC_TAG.CIMCARRIERID, webCar.CarrierID);
                        }
                        break;
                    case E_UNIT_TYPE.VEHICLE:
                        break;
                    case E_UNIT_TYPE.VIRTUAL:
                        break;
                    default:
                        break;
                }

                switch (webCar.ActionType)
                {
                    case WebCarrierDto.Action.ADD:
                        {
                            var locationUnitList = sql.Carrier.GetForLocation(webCar.UnitID);
                            if (locationUnitList.Count > 0)
                            {
                                var locationUnit = locationUnitList.FirstOrDefault();
                                Assert.IsTrue(false, "{0} Is Already Exist Carrier - {1}", locationUnit.CurrUnitID, locationUnit.ID);
                            }

                            this.InstallCarrier(webCar.CarrierID, webCar.UnitID, E_IDREAD_STATUS.SUCCESS);
                            u = sql.Unit[webCar.UnitID];
                            var c = sql.Carrier.ListN(x => x.CurrUnitID == u.ID);
                            this.SendToWeb(new WebUnitStatusDto(u.ID, u.Status, u.State, c).PushMsg);
                        }
                        break;
                    case WebCarrierDto.Action.DELETE:
                        {
                            this.RemoveCarrier(webCar.CarrierID, webCar.UnitID, E_IDREAD_STATUS.SUCCESS);
                            u = sql.Unit[webCar.UnitID];
                            var c = sql.Carrier.ListN(x => x.CurrUnitID == u.ID);
                            this.SendToWeb(new WebUnitStatusDto(u.ID, u.Status, u.State, c).PushMsg);
                        }
                        break;
                }

                if (mqmsg.IsRequest)
                {
                    mqReply.AddField("NakCode", 0);
                    this.ReplyToWeb(webCar.NakCode, mqmsg);
                }
            }
            catch (Exception e)
            {
                logger.E("[FMQ] " + e.Message + " [" + e.StackTrace + "]");

                if (mqmsg.IsRequest)
                {
                    mqReply.AddField("NakCode", "2");
                    mqReply.AddField("MSG", e.Message);
                    this.ReplyToWeb(mqReply, mqmsg);
                }
            }
        }

        #endregion
        #region COMMON 반송
        /// <summary>
        /// 반송 Rule 검사 및 생성.
        /// </summary>
        /// <param name="mqmsg"></param>
        /// <param name="cmd"></param>
        bool ExecuteCommand(TcsCommand cmd, out MQMessage mqReply)
        {
            mqReply = new MQMessage();
            TcsUnit socUnit = sql.Unit[cmd.SrcUnitID];
            TcsUnit detUnit = sql.Unit[cmd.DestUnitID];

            try
            {
                Assert.IsFalse(cmd.SrcUnitID.Equals(cmd.DestUnitID),
                    "Command={0} SourceUnit={1} DestUnit={2} same location", cmd.ID, cmd.SrcUnitID, cmd.DestUnitID);
                //Assert.IsTrue(string.IsNullOrEmpty(cmd.SrcUnitID), "{0} Srouce Unit is Empty", cmd.ID);

                if (!cmd.IsPriority)
                {
                    cmd.Priority = this.sql.AppConfig.DeaultPriority.IntValue;
                    cmd.IncPriority = this.sql.AppConfig.IncPriorityTime.IntValue;
                }

                bool hasCarrier = sql.Carrier.Has(cmd.CarrierID);
                if (hasCarrier)
                {
                    if (!cmd.IsCycle)
                    {
                        try
                        {
                            TcsCarrier c = sql.Carrier[cmd.CarrierID];
                            //2016/11/18 by mc cmd.sorceUnit is Empty 이면 빠져나가도록 변경.
                            if (string.IsNullOrEmpty(cmd.SrcUnitID) || string.IsNullOrEmpty(cmd.DestUnitID))
                            {
                                Assert.IsTrue(false, "{0} Srouce or Dest Unit is Empty", cmd.ID);
                            }
                            Assert.IsTrue(c.CurrUnitID.Equals(cmd.SrcUnitID), "NewCommand={0} Carrier Location Error", cmd.ID, cmd.SrcUnitID);
                            sql.Carrier.Update(cmd.Carrier);
                        }
                        catch (AssertException e)
                        {
                            logger.I(string.Format("{0} {1}", "[FMQ]", e.Message));
                            //sql.LogWarn.AddM();
                        }
                    }
                }
                else if ( socUnit != null )
                {
                    if ( socUnit.IsVirtual )
                    {
                        this.sql.Carrier.AddN(cmd.Carrier);
                        //this.sql.Unit.ChgStatus(cmd.SrcUnitID, E_UNIT_STATUS.FULL);
                        this.SendUnitStatusToWeb(cmd.SrcUnitID);

                        logger.I("[FMQ] Carrier not exist So, Create Carrier ID={0}", cmd.CarrierID);
                    }
                }
                else
                {
                    Assert.IsTrue(false, "{0} is not Exist", cmd.SrcUnitID);
                }

                bool isSocUnit = sql.Unit.Has(cmd.SrcUnitID);
                if (isSocUnit)
                {
                    bool isVehicle = sql.Unit[cmd.SrcUnitID].IsVehicle;
                    if (isVehicle)
                    {
                        this.ExecuteCommandByVehicle(cmd);
                        return true;
                    }
                    Assert.IsTrue(isSocUnit, "NewCommand={0} SourceUnit={1} not exist", cmd.ID, cmd.SrcUnitID);
                }


                bool isDestUnit = sql.Unit.Has(cmd.DestUnitID);
                Assert.IsTrue(isDestUnit, "NewCommand={0} DestUnit={1} not exist", cmd, cmd.DestUnitID);

                #region Source Port Check

                //2017.07.18 Toptec Kang. Source Unit이 반송 가능한 상태인지 검사한다.
                if (!socUnit.IsVirtual) // Virtual Port 일 경우 상태 확인을 안한다.
                {
                    if (socUnit.IsEmpty || !socUnit.IsReadyRun)
                        Assert.IsTrue(false, "{0} has not Carrire or not Ready to Run State", cmd.SrcUnitID);
                }
                //2017.07.19 Toptec Kang. Source Port Route Interlock Port Check
                var interlockSourcePort = sql.Unit[socUnit.ChkInterlock];
                if (interlockSourcePort != null)
                {
                    if (!interlockSourcePort.ID.Equals(detUnit.ID))
                    {
                        if (interlockSourcePort.IsFull || !interlockSourcePort.IsReadyRun)
                            Assert.IsTrue(false, "{0} Alternate Port is Full or not Ready State ! - Cmd Source [{1}] / Cmd Dest [{2}]", interlockSourcePort.ID, cmd.SrcUnitID, cmd.DestUnitID);     //170726 Kkm 자세한 LOG 확인을 위해 Source, Dest Port 추가
                    }
                }

                #endregion

                #region Dest Port Check
                if (!detUnit.IsVirtual) // Virtual Port 일 경우 상태 확인을 안한다.
                {
                    if (detUnit.IsFull || !detUnit.IsReadyRun)
                        Assert.IsTrue(false, "{0} has Carrire or not Ready to Run State", cmd.DestUnitID);
                }
                //2017.07.19 Toptec Kang. Dest Port Route Interlock Port Check
                var interlockDestPort = sql.Unit[detUnit.ChkInterlock];
                if (interlockDestPort != null)
                {
                    if (!interlockDestPort.ID.Equals(socUnit.ID))
                    {
                        if (interlockDestPort.IsFull || !interlockDestPort.IsReadyRun)
                            Assert.IsTrue(false, "{0} Alternate Port is Full or not Ready State !", interlockDestPort.ID);
                    }
                }
                #endregion

                #region carreir Data Validate
                var carrier = this.sql.Carrier[cmd.CarrierID];
                if ( carrier == null )
                    Assert.IsTrue( false, "{0} Carrier ID is not Exist !", cmd.CarrierID );

                if ( !carrier.CurrUnitID.Equals( cmd.SrcUnitID ) )
                    Assert.IsTrue( false, "{0} Carrier ID Location is Invalid !", cmd.CarrierID );
                #endregion
                //같은 Machine인지 비교
                #region Machine Check
                var machine = sql.Machine[socUnit.MachineID];
                if ( machine == null || !machine.IsCmdable )
                    Assert.IsTrue( false, "{0} Machine not Auto State", machine.ID );

                Assert.IsTrue( socUnit.MachineID.Equals( detUnit.MachineID ), "NewCommand={0} Different SourceMachine={1} and DestMachine={2}", cmd, socUnit.MachineID, detUnit.MachineID );
                #endregion

                if (!cmd.IsCommandID)
                {
                    cmd.ID = string.Format("MNL{0}{1}", socUnit.MachineID, DateTime.Now.ToString("yyyyMMddHHmmssfff"));
                    cmd.Carrier.CommandID = cmd.ID;
                    sql.Carrier.ChgCommandID(cmd.Carrier.ID, cmd.ID);
                }

                cmd.MachineID = socUnit.MachineID;
                cmd.SrcPointID = socUnit.StationID.ToString();
                cmd.DestPointID = detUnit.StationID.ToString();
                cmd.State = E_CMD_STATE.NEW;
                cmd.AddTime = DateTime.Now;


                this.sql.Command.AddN(cmd);

                if (!cmd.IsCycle)
                    this.ScreenLog(cmd.ToString());

                if (!cmd.IsCycle)
                    this.SendCommandToWeb();

                return true;
            }
            catch (Exception e)
            {
                logger.E("[FMQ] " + e.Message + " [" + e.StackTrace + "]");

                if ( socUnit.IsVirtual )
                {
                    if ( this.sql.Carrier.Has( cmd.CarrierID ) )
                        this.sql.Carrier.Del( cmd.CarrierID );
                }

                mqReply.AddField("MSG", e.Message);
                return false;
            }
        }

        //vehicle에서 반송을 시작할때.
        void ExecuteCommandByVehicle(TcsCommand cmd)
        {
            TcsUnit socVehicle = sql.Unit[cmd.SrcUnitID];

            //Vehicle에서 출발할때, Command Check 해야함.
            if (!cmd.IsCommandID)
            {
                cmd.ID = string.Format("MNL{0}{1}", socVehicle.MachineID, DateTime.Now.ToString("yyyyMMddHHmmssfff"));
                cmd.Carrier.CommandID = cmd.ID;
                sql.Carrier.Update(cmd.Carrier);
            }

            bool isDestUnit = sql.Unit.Has(cmd.DestUnitID);
            Assert.IsTrue(isDestUnit, "NewCommand={0} DestUnit={1} not exist", cmd, cmd.DestUnitID);

            //2017.07.18 Toptec Kang. Dest Unit이 반송 가능한 상태인지 검사한다.
            TcsUnit detUnit = sql.Unit[cmd.DestUnitID];
            if (!detUnit.IsVirtual) // Virtual Port 일 경우 상태 확인을 안한다.
            {
                if (detUnit.IsFull || !detUnit.IsReadyRun)
                    Assert.IsTrue(true, "{0} has Carrire or not Ready to Run State", cmd.DestUnitID);
            }

            //같은 Machine인지 비교
            Assert.IsTrue(socVehicle.MachineID.Equals(detUnit.MachineID), "NewCommand={0} Different SourceMachine={1} and DestMachine={2}", cmd, socVehicle.MachineID, detUnit.MachineID);

            cmd.MachineID = socVehicle.MachineID;
            cmd.SrcPointID = socVehicle.StationID.ToString();
            cmd.DestPointID = detUnit.StationID.ToString();
            cmd.State = E_CMD_STATE.NEW;
            cmd.AddTime = DateTime.Now;
            cmd.SocArrivedTime = DateTime.Now;
            cmd.InstallTime = DateTime.Now;

            this.sql.Command.AddN(cmd);

            this.ScreenLog(cmd.ToString());

            this.SendCommandToWeb();
        }

        #endregion

        #region Vehicle 이동거리 Reset
        void HandleWebTravelReset(MQMessage mqmsg)
        {
            string[] tmpMsg = mqmsg.GetFieldV("MSG").Split(' ');

            this.ScreenLog(string.Format("Web Message={0}", mqmsg.GetValue().ToString()));
            WebTravelResetDto dto = new WebTravelResetDto
            {
                VehicleID = tmpMsg[0],
                UserID = tmpMsg[1],
                IPAddr = tmpMsg[2],
                NakCode = 0,
            };

            bool isVehicle = sql.Unit.Has(dto.VehicleID);
            if (!isVehicle)
            {
                dto.NakCode = 2;

                this.ReplyToWeb(dto.NakCode, mqmsg);

                string content = string.Format("Vehicle={0} not exist", dto.VehicleID);
                this.sql.HisUserAction.AddHisAction(dto.UserID, dto.IPAddr, TcsUserHisAction.ActionMenu.TRAVEL_RESET, TcsUserHisAction.ActionType.UPDATE, content, "NG");

                return;
            }

            TcsUnit vehicle = sql.Unit[dto.VehicleID];
            plc.WriteBit(vehicle.MachineID + SPLIT_PREFIX + E_PLC_TAG.VEHICLETRAVELRESET, true);

            sql.Unit.ChgTotalTravelResetTime(vehicle.ID);

            dto.NakCode = 0;
            this.ReplyToWeb(dto.NakCode, mqmsg);

            this.sql.HisUserAction.AddHisAction(dto.UserID, dto.IPAddr, TcsUserHisAction.ActionMenu.TRAVEL_RESET, TcsUserHisAction.ActionType.UPDATE, "Running Reset", "OK");
        }
        #endregion

        #region PM setting/clear
        void HandleWebPM(MQMessage mqmsg)
        {
            string[] tmpMsg = mqmsg.GetFieldV("MSG").Split(' ');

            WebPMDto dto = new WebPMDto
            {
                UnitID = tmpMsg[0],
                ActionType = (WebPMDto.Action)Enum.Parse(typeof(WebPMDto.Action), tmpMsg[1]),
                NakCode = 0,
                UnitType = (E_UNIT_TYPE)Enum.Parse(typeof(E_UNIT_TYPE), tmpMsg[2]),
                UserID = tmpMsg[3],
                IPAddr = tmpMsg[4],
            };

            try
            {
                this.ScreenLog(string.Format("Web Message={0}", mqmsg.GetValue().ToString()));

                switch (dto.ActionType)
                {
                    case WebPMDto.Action.PMSETTING:
                        {
                            if (dto.UnitType != E_UNIT_TYPE.VEHICLE)
                            {
                                TcsUnit unit = sql.Unit[dto.UnitID];
                                Assert.NotNull(unit, "PM UnitID={0} not exist", dto.UnitID);

                                plc.WriteBit(unit.ID + SPLIT_PREFIX + E_PLC_TAG.PMSETTING, true);
                            }
                            else
                            {
                                TcsUnit vehicle = sql.Unit[dto.UnitID];
                                Assert.NotNull(vehicle, "PM VehicleID={0} not exist", dto.UnitID);

                                plc.WriteBit(vehicle.ID + SPLIT_PREFIX + E_PLC_TAG.PMSETTING, true);
                            }
                        }
                        break;
                    case WebPMDto.Action.PMCLEAR:
                        {
                            if (dto.UnitType != E_UNIT_TYPE.VEHICLE)
                            {
                                TcsUnit unit = sql.Unit[dto.UnitID];
                                Assert.NotNull(unit, "PM UnitID={0} not exist", dto.UnitID);

                                plc.WriteBit(unit.ID + SPLIT_PREFIX + E_PLC_TAG.PMCLEAR, true);
                            }
                            else
                            {
                                TcsUnit vehicle = sql.Unit[dto.UnitID];
                                Assert.NotNull(vehicle, "PM VehicleID={0} not exist", dto.UnitID);

                                plc.WriteBit(vehicle.ID + SPLIT_PREFIX + E_PLC_TAG.PMCLEAR, true);
                            }
                        }
                        break;
                    default:
                        break;
                }

                this.ReplyToWeb(dto.NakCode, mqmsg);
            }
            catch (Exception e)
            {
                dto.NakCode = 2;
                this.ReplyToWeb(dto.NakCode, mqmsg);

                logger.E("[FMQ] " + e.Message + " [" + e.StackTrace + "]");
            }
        }
        #endregion

        #region Vehicle Install/Remove
        private void HandleWebVehicleInstall(MQMessage mqmsg)
        {
            string[] tmpMsg = mqmsg.GetFieldV("MSG").Split(' ');

            WebVehicleInstallDto dto = new WebVehicleInstallDto
            {
                VehicleID = tmpMsg[0],
                ActionType = (E_VEHICLE_INST_STATE)Enum.Parse(typeof(E_VEHICLE_INST_STATE), tmpMsg[1]),
                UserID = tmpMsg[2],
                IPAddr = tmpMsg[3],
                NakCode = 0,
            };

            try
            {
                this.sql.Unit.ChgVehicleInstallState(dto.VehicleID, dto.ActionType);

                var v = sql.Unit[dto.VehicleID];
                if (dto.ActionType == E_VEHICLE_INST_STATE.INSTALL)
                {
                    this.EventRpt6(H_CEID.VEHICLE_INSTALL_608, v, dto.ActionType);
                    this.sql.HisUserAction.AddHisAction(dto.UserID, dto.IPAddr, TcsUserHisAction.ActionMenu.VEHICLE_INSTALL, TcsUserHisAction.ActionType.UPDATE, "Vehicle Install", "OK");
                }
                else
                {
                    this.EventRpt6(H_CEID.VEHICLE_REMOVE_609, v, dto.ActionType);
                    this.sql.HisUserAction.AddHisAction(dto.UserID, dto.IPAddr, TcsUserHisAction.ActionMenu.VEHICLE_REMOVE, TcsUserHisAction.ActionType.UPDATE, "Vehicle Remove", "OK");
                }

                this.ReplyToWeb(dto.NakCode, mqmsg);

                this.SendVehicleInstallToWeb(dto.VehicleID);
            }
            catch (Exception e)
            {
                dto.NakCode = 2;
                this.ReplyToWeb(dto.NakCode, mqmsg);

                logger.E("[FMQ] " + e.Message + " [" + e.StackTrace + "]");
            }
        }

        #endregion

        #region 반송 Schedule
        public void HandleScheduleTransfer()
        {
            if (mq == null) return;
            if (!mq.Connected)
                return;

            {
                //화면정보 주기적으로 Update Command/Unit 만 필요함
                this.SendCommandToWeb();
            }

            //IList<TcsVehicle> vehicles = this.sql.Vehicle.All;
            //foreach (var v in vehicles)
            //{
            //    var m = this.sql.Machine[v.MachineID];

            //    if (m.ConnState == E_CONN_STATE.DIS_CONNECT)
            //        continue;

            //    if (m.State == E_MACHINE_STATE.MANUAL)
            //        continue;

            //    if (v.State != E_STATE.NORMAL)
            //        continue;

            //    if (v.InstallState == E_VEHICLE_INST_STATE.REMOVE)
            //        continue;

            //    //현재 Vehicle 반송중일 경우 skip
            //    if (this.sql.Command.HasTransferByVehicleID(v.ID))
            //        continue;

            //    //현재 Machine이 작업 가능 반송있는 여부 체크 없을 경우 skip
            //    bool isCmd = this.sql.Command.HasFirstByMachineID(v.MachineID);
            //    if (!isCmd)
            //        continue;

            //    TcsCommand cmd = this.sql.Command.GetFirstByMachineID(v.MachineID);

            //    //source unit
            //    //출발이 Unit일때는 Unit만 Check 하면됨, Vehicle에서 출발은 Check하지않는다, 앞에서 Check 함.
            //    bool hasUnit = sql.Unit.Has(cmd.SrcUnitID);
            //    if (hasUnit)
            //    {
            //        TcsUnit socUnit = sql.Unit[cmd.SrcUnitID];

            //        //Soc UNIT이 PM, OUTSERVICE 이면 작업하지 않는다.
            //        if (socUnit.State != E_STATE.NORMAL) continue;
            //    }

            //    TcsUnit destUnit = sql.Unit[cmd.DestUnitID];
            //    //Dest UNIT이 PM, OUTSERVICE 이면 작업하지 않는다.
            //    if (destUnit.State != E_STATE.NORMAL) continue;

            //    //var nt = sql.Nss.BeginTrx();
            //    try
            //    {
            //        this.sql.Command.ChgState(cmd.ID, E_CMD_STATE.ORDER);
            //        this.sql.Command.ChgVehicleID(cmd.ID, v.ID);

            //        cmd = this.sql.Command[cmd.ID];

            //        //반송을 내릴때, Carrier에 Command을 생성한다.
            //        sql.Carrier.ChgCommandID(cmd.CarrierID, cmd.ID);

            //        plc.WriteWord(m.ID + SPLIT_PREFIX + E_PLC_TAG.CIMCARRIERID, cmd.CarrierID);
            //        plc.WriteWord(m.ID + SPLIT_PREFIX + E_PLC_TAG.CIMFROM, cmd.SrcPointID);
            //        plc.WriteWord(m.ID + SPLIT_PREFIX + E_PLC_TAG.CIMTO, cmd.DestPointID);
            //        plc.WriteBit(m.ID + SPLIT_PREFIX + E_PLC_TAG.COMMAND, true);

            //        this.ScreenLog(string.Format("Transfer Send To PLC carrierID={0} Soc={1} Dest={2}", cmd.CarrierID, cmd.SrcUnitID, cmd.DestUnitID));

            //        logger.I("Transfer Send To PLC carrierID={0} Soc={1} Dest={2}", cmd.CarrierID, cmd.SrcUnitID, cmd.DestUnitID);
            //        //sql.Nss.Commit(nt);
            //    }
            //    catch (Exception e)
            //    {
            //        logger.E(e);
            //        //sql.Nss.Rollback(nt);
            //        this.sql.Command.ChgState(cmd.ID, E_CMD_STATE.NEW);
            //    }
            //}

            //incPriority는 지연 시간 분으로 변경한다.
            int incPriority = this.sql.AppConfig.IncPriorityTime.IntValue;
            this.sql.Command.ChgIncPriority(incPriority);
        }

        private void HandleAlternatePortAutoTransfer()
        {
            try
            {
                var alternatePortLIst = sql.Unit.All.Where( x => x.UnitType == E_UNIT_TYPE.PORT_MCT && x.IsFull );

                alternatePortLIst.FwEach( x =>
                {
                    var carrier = sql.Carrier.GetCarrierByUnit( x.ID );
                    if ( carrier == null ) return;

                    if ( DateTime.Now.Subtract( carrier.AddTime ).Minutes < 10 ) return;//설정 으로 바꾸자.

                    if ( sql.Command.HasByCarrierID( carrier.ID ) ) return;

                    if ( sql.Command.HasFirstByMachineID( x.MachineID ) ) return;

                    var emptyUnitList = sql.Unit.All.Where( u => u.MachineID == x.MachineID && u.UnitType == E_UNIT_TYPE.PORT_B1 || u.UnitType == E_UNIT_TYPE.PORT_B2 && u.IsEmpty );
                    if ( emptyUnitList == null ) return;

                    var machineType = sql.Machine.GetByUnitID( x.ID ).Type;

                    TcsCommand cmd = new TcsCommand
                    {
                        CreateUserID = "Auto Transfer",
                        SrcUnitID = x.ID,
                        CarrierID = carrier.ID,
                        Carrier = carrier,
                        NakCode = 0,
                    };

                    switch ( machineType )
                    {
                        case TcsMachine.MACHINE_TYPE.TA:
                            {
                                var eB02 = emptyUnitList.Where( eb => eb.UnitType == E_UNIT_TYPE.PORT_B2 ).FirstOrDefault();
                                var eB01 = emptyUnitList.Where( eb => eb.UnitType == E_UNIT_TYPE.PORT_B1 ).FirstOrDefault();
                                if ( eB01 == null ) return;

                                cmd.DestUnitID = eB02 == null ? eB01.ID : eB02.ID;
                            }
                            break;
                        case TcsMachine.MACHINE_TYPE.TB:
                        case TcsMachine.MACHINE_TYPE.SA:
                            {
                                cmd.DestUnitID = emptyUnitList.FirstOrDefault().ID;
                            }
                            break;
                        default:
                            break;
                    }

                    CreateTransferCommand( cmd );
                } );
            }
            catch (Exception ex)
            {
                logger.E( string.Format( "[FMQ] {0} - {1}", ex.Message, ex.StackTrace ) );
            }
        }

        bool CreateTransferCommand( TcsCommand cmd )
        {
            WebCommandDto dto = new WebCommandDto
            {
                CarrierID = cmd.CarrierID,
                SrcUnit = cmd.SrcUnitID,
                DestUnit = cmd.DestUnitID,
                UserID = cmd.CreateUserID,
                IPAddr = string.Format( "127.0.0.1" ),
            };

            cmd.FinalLocation = cmd.DestUnitID;

            bool isCommand = this.sql.Command.HasByCarrierID( cmd.CarrierID );
            if ( isCommand )
            {
                cmd.NakCode = 2;
                return false;
            }

            //단위반송
            cmd.Carrier = new TcsCarrier
            {
                ID = cmd.CarrierID,
                CommandID = cmd.ID,
                CurrUnitID = cmd.SrcUnitID,
            };
            MQMessage mqReply = null;
            bool isCreate = ExecuteCommand( cmd, out mqReply );
            //cmd.NakCode = isCreate ? 0 : 2;            
            mqReply.AddField( "NakCode", isCreate ? 0 : 2 );

            if ( isCreate )
            {
                TcsCommand ncmd = this.sql.Command.All.Where( x => x.CarrierID == cmd.CarrierID ).First();
                this.EventRpt9( H_CEID.OPERATOR_INIT_ACTION_501, ncmd, H_OPER_INIT.TRANSFER );

                string content = string.Format( "Carrier={0} Command Create", ncmd.CarrierID );
                this.sql.HisUserAction.AddHisAction( dto, TcsUserHisAction.ActionMenu.COMMAND, TcsUserHisAction.ActionType.ADD, content, "OK" );
            }
            else
            {
                this.sql.Command.DeleteByCarrierID( cmd.CarrierID );
                this.DeleteVirtualPortData( cmd.Carrier );

                string content = mqReply.GetFieldV( "MSG" );
                this.sql.HisUserAction.AddHisAction( dto, TcsUserHisAction.ActionMenu.COMMAND, TcsUserHisAction.ActionType.ADD, content, "NG" );

                return false;
            }

            return true;
        }
        #endregion

    }
}