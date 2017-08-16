using System;
using System.Collections.Generic;
using System.Linq;
using Floware.Concurrent;
using Floware.PLC.Model;
using Floware.Utils;
using TcsV2.Core;
using TcsV2.Core.MODEL;


namespace TCS.ServerV2
{

    partial class FormMain
    {
        const int WAIT_VRP = 1000;

        //private int WAIT_VRP_OHS { get; set; }
        //const int WAIT_VRP_OHS = ConstUtils.ONE_SECOND * 300;
        //const int WAIT_VRP_WAIT = ConstUtils.ONE_SECOND * 300;

        //const int WAIT_VRP_MIDDLE = ConstUtils.ONE_SECOND * 60;
        //const int WAIT_VRP_LONG = ConstUtils.ONE_SECOND * 120;

        //const int WAIT_VRP_SHORT = ConstUtils.ONE_SECOND * 40;
        //const int WAIT_VRP_MOVE_REQ = ConstUtils.ONE_SECOND * 2;

        void InitVrp(string vhid)
        {
            logger.W("VRP Start... VehicleID : {0} ", vhid);

            for (; ; )
            {
                try
                {
                    if (sql.HsmsConfig.ConnectState.Value.Equals(E_CONN_STATE.DIS_CONNECT) || sql.HsmsConfig.ControlState.Value.Equals(E_CONTROLSTATE.OFFLINE))
                    {
                        logger.W("[VRP] HOST connection is OFFLINE or TCS Offline");
                        continue;
                    }

                    var vh = sql.Unit[vhid];

                    var machine = sql.Machine[vh.MachineID];

                    var assVh = sql.SubCmd[vhid];
                    if (null == assVh)
                        continue;

                    var cmd = sql.Command[assVh.CmdID];
                    if (null == cmd)
                    {
                        continue;
                    }

                    if (cmd.Assigned)
                    {
                        logger.W("[{0}] Command Assign Fail : {1}", vhid, cmd.ID);
                        continue;
                    }
                    if (!machine.IsCmdable)
                    {
                        logger.W("[{0}] NOT Commandable Status", machine.ID);
                        continue;
                    }
                    if (!vh.CanCmdable)
                    {
                        logger.W("[{0}] NOT Commandable Status", vh.ID);
                        continue;
                    }

                    var carrier = sql.Carrier[cmd.CarrierID];

                    var route = assVh.Route;
                    var ll = route.Split(',').Select(x => sql.Unit[x]).ToList();

                    var from = ll[0];
                    var to = ll[1];

                    if (!plc.ReadBit(vh.ID + "#CMD_READY").IsBitOn)
                    {
                        logger.W("[{0}] NOT Commandable Status", vh.ID);
                        continue;
                    }
                    //현재 carrier loc 과 form 비교 조건문 추가.
                    if (carrier.CurrUnitID == from.ID)
                    {
                        if (!from.CanUnload)
                        {
                            logger.W("[{0}] From UnitID {1} Check... State : {2}, Status : {3}",
                                vhid, from.ID, from.State, from.Status);
                            continue;
                        }
                    }

                    if (!to.CanLoad)
                    {
                        logger.W("[{0}] To UnitID {1} Check... State : {2}, Status : {3}",
                            vhid, to.ID, to.State, to.Status);
                        continue;
                    }

                    if (!from.ID.Equals(carrier.CurrUnitID))
                    {
                        logger.W("[{0}] fromUnit[{1}] not Equal Carrier Location[{2}]", vhid, from.ID, carrier.CurrUnitID);
                        continue;
                    }

                    BitBlock awoken = null;

                    bool waked = false;
                    if (from.UnitType == E_UNIT_TYPE.PORT_MCT || to.UnitType == E_UNIT_TYPE.PORT_MCT)
                    {
                        if (!plc.ReadBit(vh.OtherVehicleID + "#ARM_FOLD").IsBitOn)
                        {
                            logger.W("[{0}] ARM Check Fail : {1}", vhid, vh.OtherVehicleID);
                            continue;
                        }
                    }

                    this.sql.Command.ChgVehicleID(cmd.ID, vh.ID);
                    this.sql.Command.ChgAssigned(cmd.ID, true);
                    this.SendVehicleSubStateToWeb(vhid);

                    logger.W("[{0}] ASSIGNED COMMANDID : {1}", vhid, cmd.ID);

                    sql.Carrier.ChgCommandID(cmd.CarrierID, cmd.ID);
                    
                    //Transfer Init
                    if (cmd.SrcUnitID.Equals(from.ID) && cmd.State == E_CMD_STATE.NEW)
                    {
                        sql.Command.ChgState(cmd.ID, E_CMD_STATE.TRANSFER_INIT);
                        this.EventRpt3(H_CEID.TRANSFER_INIT_208, cmd);
                        this.SendDriectionToWeb(cmd.SrcUnitID, cmd.DestUnitID);
                        sql.Command.ChgState(cmd.ID, E_CMD_STATE.ASSIGN);
                        sql.Command.ChgAssignTime(cmd.ID);
                        this.EventRpt6(H_CEID.VEHICLE_ASSIGN_604, cmd, from.ID);
                        logger.W("[{0}] SEND TRANSFERINIT CommandID : {1}, CarrierID : {2}", vhid, cmd.ID, carrier.ID);
                    }

                    sql.Unit.ChgSubState(vhid, E_VEHICLE_SUB_STATE.ENROUTE);

                    logger.W("[{0}] WRITE to MOVE_REQ from : {1}, to : {2}, PlcCarrierID : {3}", vhid, from.StationID, to.StationID, cmd.Carrier.plcCarrier);
                    plc.WriteWord(vhid + "#CIMFROM", from.StationID);
                    plc.WriteWord(vhid + "#CIMTO", to.StationID);

                    string plcCarrier = cmd.Carrier.plcCarrier;
                    plc.WriteWord(vhid + "#CIMCARRIERID", plcCarrier);
                    LockUtils.Wait(500);
                    plc.WriteBit(vhid + "#MOVE_REQ", true);

                    try
                    {
                        waked = plc.WaitChgBits(true, WAIT_VRP_MOVEREQ, out awoken, vhid + "#CMD_NAK", vhid + "#ABORT");
                        Assert.IsFalse(waked, "[{0}] Vrp MOVE_REQ NAK", vhid);
                    }
                    catch (AssertException e)
                    {
                        logger.E("[{0}] {1}", vhid, e.Message);
                        this.HostCancel(cmd, true, "TCS", "");
                        continue;
                    }

                    if (!from.IsVehicle)
                    {
                        #region Vehicle ACQ Start
                        try
                        {
                            logger.W("[{0}] WAIT ACQ-START CommandID : {1}", vhid, cmd.ID);
                            waked = plc.WaitChgBits(true, from.UnitType == E_UNIT_TYPE.PORT_OHS ? WAIT_VRP_OHS : WAIT_VRP_VEHICLE, out awoken, vhid + "#ACQ_START", vhid + "#ACQ_FAIL", vhid + "#PIO_ERROR", vhid + "#ABORT", vhid + "#MOVE_REQ_ABORT");
                            //Assert.IsTrue(waked, string.Format("[{0}] Vrp ACQ-START timeout}", vhid));
                            Assert.IsTrue(waked, "[{0}] Vrp ACQ-START timeout", vhid);

                        }
                        catch (AssertException e)
                        {
                            logger.E("[{0}] {1}", vhid, e.Message);
                            SendVehicleStatusToWeb(vh.ID);
                            ValidAbortCode(cmd, vh, from, true);
                            continue;
                        }

                        switch (awoken.SubNo)
                        {
                            //ACQ_START
                            case 1:
                                logger.W("[{0}] RESULT ACQ-START COMPLETE CommandID : {1}", vhid, cmd.ID);
                                if (from.ID.Equals(cmd.SrcUnitID))
                                {
                                    this.sql.Command.ChgState(cmd.ID, E_CMD_STATE.TRANSFERRING);
                                    this.sql.Command.ChgHostState(cmd.ID, H_HOST_STATE.PROCESSING);
                                    this.EventRpt3(H_CEID.TRANSFERRING_211, cmd);
                                }

                                this.sql.Command.ChgState(cmd.ID, E_CMD_STATE.ACQ_START);
                                this.sql.Unit.ChgSubState(vh.ID, E_VEHICLE_SUB_STATE.ACQUIRING);
                                this.SendVehicleSubStateToWeb(vh.ID, from.ID);
                                this.EventRpt6(H_CEID.VEHICLE_ACQ_START_602, cmd, from.ID);
                                break;
                            //ACQ_FAIL    
                            case 3:
                                logger.E("[{0}] RESULT ACQ-START FAIL CommandID : {1}", vhid, cmd.ID);
                                this.sql.Unit.ChgSubState(vh.ID, E_VEHICLE_SUB_STATE.NOTASSIGN);
                                SendVehicleStatusToWeb(vh.ID);
                                this.SendVehicleSubStateToWeb(vh.ID);
                                this.EventRpt6(H_CEID.VEHICLE_UNASSIGN_610, cmd, from.ID);
                                ValidResultCode(cmd, vh, from);
                                continue;
                            //Source PIO_ERROR
                            case 4:
                                logger.W("[{0}] RESULT ACQ START PIO ERROR CommandID : {1}", vhid, cmd.ID);
                                this.sql.Unit.ChgSubState(vh.ID, E_VEHICLE_SUB_STATE.NOTASSIGN);
                                SendVehicleStatusToWeb(vh.ID);
                                this.SendVehicleSubStateToWeb(vh.ID);
                                this.EventRpt6(H_CEID.VEHICLE_UNASSIGN_610, cmd, from.ID);
                                ValidResultCode(cmd, vh, from);
                                continue;
                            //ABORT
                            case 6:
                                ValidAbortCode( cmd, vh, from, true );
                                SendVehicleStatusToWeb( vh.ID);
                                logger.W("[{0}] RESULT ACQ-START ABORT CommandID : {1}", vhid, cmd.ID);
                                continue;
                            //EQP ABORT (MOVE_REQ_ABORT)
                            case 7:
                                ValidAbortCode( cmd, vh, from, true );
                                SendVehicleStatusToWeb( vh.ID);
                                logger.W("[{0}] MOVE_REQ_ABORT CommandID:{1}", vhid, cmd.ID);
                                continue;
                            default:
                                logger.W("[{0}] RESULT ACQ-START UNKNOWN CommandID : {1}", vhid, cmd.ID);
                                this.sql.Unit.ChgSubState(vh.ID, E_VEHICLE_SUB_STATE.NOTASSIGN);
                                SendVehicleStatusToWeb(vh.ID);
                                this.SendVehicleSubStateToWeb(vh.ID);
                                this.EventRpt6(H_CEID.VEHICLE_UNASSIGN_610, cmd, from.ID);
                                ValidResultCode(cmd, vh, from);
                                continue;
                        }
                        #endregion

                        #region Carrier_Install
                        try
                        {
                            var carrierInstallTimeout = 60000; //ACQ 시작이후 1분 동안 Install을 기다린다.
                            logger.W("[{0}] WAIT CARRIER-INSTALL CommandID : {1}", vhid, cmd.ID);
                            waked = plc.WaitChgBits(true, carrierInstallTimeout, out awoken, vhid + "#CARRIER_INSTALL", vhid + "#ABORT", vhid + "#ACQ_FAIL", vhid + "#PIO_ERROR", vhid + "#MOVE_REQ_ABORT");
                            Assert.IsTrue(waked, "[{0}] Vrp CARRIER_INSTALL timeout", vhid);
                        }
                        catch (AssertException e)
                        {
                            logger.E("[{0}] {1}", vhid, e.Message);
                            SendVehicleStatusToWeb(vh.ID);
                            ValidAbortCode(cmd, vh, from, true);
                            continue;
                        }

                        switch (awoken.SubNo)
                        {
                            case 0:
                                {
                                    logger.W("[{0}] RESULT CARRIER-INSTALL COMPLETE CommandID : {1}", vhid, cmd.ID);
                                    this.sql.Command.ChgInstallTime(cmd.ID);
                                    this.sql.Command.ChgState(cmd.ID, E_CMD_STATE.INSTALLED);
                                    this.sql.Carrier.ChgLocation(cmd.CarrierID, vhid);
                                    //this.sql.Unit.ChgStatus(vhid, E_UNIT_STATUS.FULL);
                                    this.EventRpt4(H_CEID.CARRIERINSTALL_301, sql.Carrier[cmd.CarrierID], E_IDREAD_STATUS.SUCCESS);
                                    
                                    //if (from.IsVirtual)
                                    //{
                                    //    logger.W("[{0}] Vitual Port({1}) Status to {2}", vhid, from.ID, E_UNIT_STATUS.EMPTY);
                                    //    sql.Unit.ChgStatus(from.ID, E_UNIT_STATUS.EMPTY);
                                    //    this.SendUnitStatusToWeb(from.ID);
                                    //}

                                    this.SendVehicleStatusToWeb(vhid);
                                }
                                break;
                            case 3:
                                logger.W("[{0}] RESULT CARRIER-INSTALL FAIL CommandID : {1}", vhid, cmd.ID);
                                this.sql.Unit.ChgSubState(vh.ID, E_VEHICLE_SUB_STATE.NOTASSIGN);
                                SendVehicleStatusToWeb(vh.ID);
                                this.SendVehicleSubStateToWeb(vhid);
                                this.EventRpt6(H_CEID.VEHICLE_UNASSIGN_610, cmd, from.ID);
                                ValidResultCode(cmd, vh, from);
                                continue;
                            case 6:
                                ValidAbortCode( cmd, vh, from, true );
                                SendVehicleStatusToWeb( vh.ID );
                                logger.W("[{0}] RESULT CARRIER-INSTALL ABORT CommandID : {1}", vhid, cmd.ID);
                                continue;
                            //MOVE_REQ_ABORT
                            case 7:
                                ValidAbortCode( cmd, vh, from, true );
                                SendVehicleStatusToWeb( vh.ID);
                                logger.W("[{0}] MOVE_REQ_ABORT CommandID:{1}", vhid, cmd.ID);
                                continue;
                            default:
                                logger.W("[{0}] RESULT CARRIER-INSTALL UNKNOWN CommandID : {1}", vhid, cmd.ID);
                                this.sql.Unit.ChgSubState(vhid, E_VEHICLE_SUB_STATE.NOTASSIGN);
                                SendVehicleStatusToWeb(vhid);
                                this.SendVehicleSubStateToWeb(vhid);
                                this.EventRpt6(H_CEID.VEHICLE_UNASSIGN_610, cmd, from.ID);
                                ValidResultCode(cmd, vh, from);
                                continue;
                        }
                        #endregion

                        #region Vehicle ACQ Complte
                        try
                        {
                            logger.W("[{0}] WAIT ACQ-COMP CommandID : {1}", vhid, cmd.ID);
                            waked = plc.WaitChgBits(true, WAIT_VRP_VEHICLE, out awoken, vhid + "#ACQ_COMP", vhid + "#ACQ_FAIL", vhid + "#PIO_ERROR", vhid + "#ABORT", vhid + "#MOVE_REQ_ABORT");
                            Assert.IsTrue(waked, "[{0}] Vrp ACQ COMP timeout", vhid);
                        }
                        catch (AssertException e)
                        {
                            logger.W("[{0}] {1}", vhid, e.Message);
                            SendVehicleStatusToWeb(vh.ID);
                            ValidAbortCode(cmd, vh, from, true);
                            continue;
                        }

                        switch (awoken.SubNo)
                        {
                            //ACQ_COMP
                            case 2:
                                logger.W("[{0}] RESULT ACQ-COMP COMPLETE  CommandID : {1}", vhid, cmd.ID);
                                this.sql.Command.ChgState(cmd.ID, E_CMD_STATE.ACQ_COMP);
                                this.sql.Unit.ChgSubState(vh.ID, E_VEHICLE_SUB_STATE.PARKED);
                                this.SendVehicleSubStateToWeb(vh.ID, from.ID);
                                this.EventRpt6(H_CEID.VEHICLE_ACQ_COMP_603, cmd, from.ID);
                                break;
                            //ACQ_FAIL    
                            case 3:
                                logger.W("[{0}] RESULT ACQ-COMP FAIL  CommandID : {1}", vhid, cmd.ID);
                                this.sql.Unit.ChgSubState(vh.ID, E_VEHICLE_SUB_STATE.NOTASSIGN);
                                SendVehicleStatusToWeb(vhid);
                                this.SendVehicleSubStateToWeb(vh.ID);
                                this.EventRpt6(H_CEID.VEHICLE_UNASSIGN_610, cmd, from.ID);
                                ValidResultCode(cmd, vh, from);
                                continue;
                            //PIO_ERROR
                            case 4:
                                logger.W("[{0}] RESULT ACQ-COMP PIO-ERROR  CommandID : {1}", vhid, cmd.ID);
                                this.sql.Unit.ChgSubState(vh.ID, E_VEHICLE_SUB_STATE.NOTASSIGN);
                                SendVehicleStatusToWeb(vhid);
                                this.SendVehicleSubStateToWeb(vh.ID);
                                this.EventRpt6(H_CEID.VEHICLE_UNASSIGN_610, cmd, from.ID);
                                ValidResultCode(cmd, vh, from);
                                continue;
                            //ABORT
                            case 6:
                                ValidAbortCode( cmd, vh, from, true );
                                logger.W("[{0}] RESULT ACQ-COMP ABORT  CommandID : {1}", vhid, cmd.ID);
                                SendVehicleStatusToWeb(vh.ID);
                                continue;
                            //MOVE_REQ_ABORT
                            case 7:
                                ValidAbortCode( cmd, vh, from, true );
                                SendVehicleStatusToWeb( vh.ID);
                                logger.W("[{0}] MOVE_REQ_ABORT CommandID:{1}", vhid, cmd.ID);
                                continue;
                            default:
                                logger.W("[{0}] RESULT ACQ-COMP UNKNOWN CommandID : {1}", vhid, cmd.ID);
                                this.sql.Unit.ChgSubState(vh.ID, E_VEHICLE_SUB_STATE.NOTASSIGN);
                                SendVehicleStatusToWeb(vhid);
                                this.SendVehicleSubStateToWeb(vh.ID);
                                this.EventRpt6(H_CEID.VEHICLE_UNASSIGN_610, cmd, from.ID);
                                ValidResultCode(cmd, vh, from);
                                continue;
                        }
                        #endregion
                    }
                    //Vehicle Dep Start
                    #region Vehicle Dep Start
                    try
                    {
                        logger.W("[{0}] WAIT DEP-START CommandID : {1}", vhid, cmd.ID);
                        waked = plc.WaitChgBits(true, to.UnitType == E_UNIT_TYPE.PORT_OHS ? WAIT_VRP_OHS : WAIT_VRP_VEHICLE, out awoken, vhid + "#DEP_START", vhid + "#DEP_FAIL", vhid + "#PIO_ERROR", vhid + "#ABORT", vhid + "#MOVE_REQ_ABORT");
                        Assert.IsTrue(waked, "[{0}] Vrp DEP-START timeout", vhid);
                    }
                    catch (AssertException e)
                    {
                        logger.W("[{0}] {1}", vhid, e.Message);
                        SendVehicleStatusToWeb(vh.ID);
                        ValidAbortCode(cmd, vh, to, true);
                        continue;
                    }

                    switch (awoken.SubNo)
                    {
                        //DEP_START
                        case 1:
                            logger.W("[{0}] RESULT DEP-START COMPLETE CommandID : {1}", vhid, cmd.ID);
                            this.sql.Command.ChgState(cmd.ID, E_CMD_STATE.DEP_START);
                            this.sql.Unit.ChgSubState(vh.ID, E_VEHICLE_SUB_STATE.DEPOSITING);
                            this.SendVehicleSubStateToWeb(vh.ID, to.ID);
                            this.EventRpt6(H_CEID.VEHICLE_DEP_START_606, cmd, to.ID);
                            break;
                        //DEP_FAIL    
                        case 3:
                            logger.W("[{0}] RESULT DEP-START FAIL CommandID : {1}", vhid, cmd.ID);
                            this.sql.Command.ChgState(cmd.ID, E_CMD_STATE.DEP_FAIL);
                            this.sql.Unit.ChgSubState(vh.ID, E_VEHICLE_SUB_STATE.NOTASSIGN);
                            SendVehicleStatusToWeb(vhid);
                            this.SendVehicleSubStateToWeb(vh.ID);
                            this.EventRpt6(H_CEID.VEHICLE_UNASSIGN_610, cmd, to.ID);
                            if (vh.UnitType == E_UNIT_TYPE.MCT)
                            ValidResultCode(cmd, vh, to);
                            continue;
                        //PIO_ERROR
                        case 4:
                            logger.W("[{0}] RESULT DEP-START PIO-ERROR CommandID : {1}", vhid, cmd.ID);
                            this.sql.Unit.ChgSubState(vh.ID, E_VEHICLE_SUB_STATE.NOTASSIGN);
                            SendVehicleStatusToWeb(vhid);
                            this.SendVehicleSubStateToWeb(vh.ID);
                            this.EventRpt6(H_CEID.VEHICLE_UNASSIGN_610, cmd, to.ID);
                            ValidResultCode(cmd, vh, to);
                            continue;
                        //ABORT
                        case 6:
                            ValidAbortCode( cmd, vh, to, true );
                            logger.W("[{0}] RESULT DEP-START ABORT CommandID : {1}", vhid, cmd.ID);
                            SendVehicleStatusToWeb(vh.ID);
                            continue;
                        //MOVE_REQ_ABORT
                        case 7:
                            ValidAbortCode( cmd, vh, to, true );
                            SendVehicleStatusToWeb( vh.ID);
                            logger.W("[{0}] MOVE_REQ_ABORT CommandID:{1}", vhid, cmd.ID);
                            continue;
                        default:
                            logger.W("[{0}] RESULT DEP-START UNKNOWN CommandID : {1}", vhid, cmd.ID);
                            this.sql.Unit.ChgSubState(vh.ID, E_VEHICLE_SUB_STATE.NOTASSIGN);
                            SendVehicleStatusToWeb(vhid);
                            this.SendVehicleSubStateToWeb(vh.ID);
                            this.EventRpt6(H_CEID.VEHICLE_UNASSIGN_610, cmd, to.ID);
                            ValidResultCode(cmd, vh, to);
                            continue;
                    }
                    #endregion

                    #region Carrier_Removed
                    try
                    {
                        logger.W("[{0}] WAIT CARRIER-REMOVED CommandID : {1}", vhid, cmd.ID);
                        waked = plc.WaitChgBits(true, WAIT_VRP_VEHICLE, out awoken, vhid + "#CARRIER_REMOVED", vhid + "#DEP_FAIL", vhid + "#PIO_ERROR", vhid + "#ABORT", vhid + "#MOVE_REQ_ABORT");
                        Assert.IsTrue(waked, "[{0}] Vrp CARRIER REMOVED timeout", vhid);
                    }
                    catch (AssertException e)
                    {
                        logger.W("[{0}] {1}", vhid, e.Message);
                        SendVehicleStatusToWeb(vh.ID);
                        ValidAbortCode(cmd, vh, to, true);
                        continue;
                    }

                    switch (awoken.SubNo)
                    {
                        case 0:
                            {
                                logger.W("[{0}] RESULT CARRIER-REMOVED COMPLETE CommandID : {1}", vhid, cmd.ID);
                                this.sql.Command.ChgRemoveTime(cmd.ID);
                                this.sql.Command.ChgState(cmd.ID, E_CMD_STATE.REMOVED);
                                //this.sql.Unit.ChgStatus(vh.ID, E_UNIT_STATUS.EMPTY);

                                this.EventRpt4(H_CEID.CARRIERREMOVED_302, sql.Carrier[cmd.CarrierID], E_IDREAD_STATUS.SUCCESS);

                                this.sql.Carrier.ChgLocation(cmd.CarrierID, to.ID);

                                //if (to.IsVirtual)
                                //{
                                //    logger.W("[{0}] Vitual Port Status to {1}", vhid, E_UNIT_STATUS.FULL);
                                //    sql.Unit.ChgStatus(to.ID, E_UNIT_STATUS.FULL);
                                //    this.SendUnitStatusToWeb(to.ID);
                                //}
                                this.SendVehicleStatusToWeb(vh.ID);
                            }
                            break;
                        //DEP_FAIL    
                        case 3:
                            logger.W("[{0}] RESULT CARRIER-REMOVED FAIL CommandID : {1}", vhid, cmd.ID);
                            this.sql.Unit.ChgSubState(vh.ID, E_VEHICLE_SUB_STATE.NOTASSIGN);
                            SendVehicleStatusToWeb(vhid);
                            this.SendVehicleSubStateToWeb(vh.ID);
                            this.EventRpt6(H_CEID.VEHICLE_UNASSIGN_610, cmd, to.ID);
                            ValidResultCode(cmd, vh, to);
                            continue;
                        //ABORT
                        case 6:
                            logger.W("[{0}] RESULT CARRIER-REMOVED ABORT CommandID : {1}", vhid, cmd.ID);
                            ValidAbortCode(cmd, vh, to, true);
                            SendVehicleStatusToWeb( vh.ID );
                            continue;
                        //MOVE_REQ_ABORT
                        case 7:
                            SendVehicleStatusToWeb(vh.ID);
                            ValidAbortCode( cmd, vh, to, true );
                            logger.W("[{0}] MOVE_REQ_ABORT CommandID:{1}", vhid, cmd.ID);
                            continue;
                        default:
                            this.sql.Unit.ChgSubState(vh.ID, E_VEHICLE_SUB_STATE.NOTASSIGN);
                            this.EventRpt6(H_CEID.VEHICLE_UNASSIGN_610, cmd, to.ID);
                            ValidResultCode(cmd, vh, to);
                            SendVehicleStatusToWeb( vhid );
                            this.SendVehicleSubStateToWeb( vh.ID );
                            continue;
                    }

                    //Vehicle Dep Comp
                    try
                    {
                        logger.W("[{0}] WAIT DEP-COMP CommandID : {1}", vhid, cmd.ID);
                        waked = plc.WaitChgBits(true, WAIT_VRP_VEHICLE, out awoken, vhid + "#DEP_COMP", vhid + "#DEP_FAIL", vhid + "#PIO_ERROR", vhid + "#ABORT", vhid + "#MOVE_REQ_ABORT");
                        Assert.IsTrue(waked, "[{0}] Vrp DEPOSIT COMPLETE timeout", vhid);
                    }
                    catch (AssertException e)
                    {
                        logger.W("[{0}] {1}", vhid, e.Message);
                        SendVehicleStatusToWeb(vh.ID);
                        ValidResultCode(cmd, vh, to);
                        continue;
                    }

                    //from.ChkInterlock = false;
                    switch (awoken.SubNo)
                    //switch (awoken.Name.Split('#').Last())
                    {
                        //DEP_COMP
                        case 2:
                            logger.W("[{0}] RESULT DEP-COMP COMPLETE CommandID : {1}", vhid, cmd.ID);
                            this.sql.Command.ChgState(cmd.ID, E_CMD_STATE.DEP_COMP);
                            this.sql.Unit.ChgSubState(vh.ID, E_VEHICLE_SUB_STATE.PARKED);
                            this.SendVehicleSubStateToWeb(vh.ID, to.ID);
                            this.EventRpt6(H_CEID.VEHICLE_DEP_COMP_607, cmd, to.ID);
                            break;
                        //DEP_FAIL    
                        case 3:
                            logger.W("[{0}] RESULT DEP-COMP FAIL CommandID : {1}", vhid, cmd.ID);
                            this.sql.Unit.ChgSubState(vh.ID, E_VEHICLE_SUB_STATE.NOTASSIGN);
                            SendVehicleStatusToWeb(vhid);
                            this.SendVehicleSubStateToWeb(vh.ID);
                            logger.W("[{0}] UNASSIGNED..", vhid);
                            this.EventRpt6(H_CEID.VEHICLE_UNASSIGN_610, cmd, to.ID);
                            ValidResultCode(cmd, vh, to);
                            continue;
                        //PIO_ERROR
                        case 4:
                            logger.W("[{0}] RESULT DEP-COMP PIO-ERROR CommandID : {1}", vhid, cmd.ID);
                            this.sql.Unit.ChgSubState(vh.ID, E_VEHICLE_SUB_STATE.NOTASSIGN);
                            SendVehicleStatusToWeb(vhid);
                            this.SendVehicleSubStateToWeb(vh.ID);
                            logger.W("[{0}] UNASSIGNED..", vhid);
                            this.EventRpt6(H_CEID.VEHICLE_UNASSIGN_610, cmd, to.ID);
                            ValidResultCode(cmd, vh, to);
                            continue;
                        //ABORT
                        case 6:
                            logger.W("[{0}] RESULT DEP-COMP ABORT CommandID : {1}", vhid, cmd.ID);
                            ValidAbortCode(cmd, vh, to, true);
                            SendVehicleStatusToWeb( vh.ID );
                            continue;
                        //MOVE_REQ_ABORT
                        case 7:
                            logger.W("[{0}] MOVE_REQ_ABORT CommandID:{1}", vhid, cmd.ID);
                            ValidAbortCode( cmd, vh, to, true );
                            SendVehicleStatusToWeb( vh.ID );
                            continue;
                        default:
                            //break;
                            this.sql.Unit.ChgSubState(vh.ID, E_VEHICLE_SUB_STATE.NOTASSIGN);
                            logger.W("[{0}] UNASSIGNED..", vhid);
                            this.EventRpt6(H_CEID.VEHICLE_UNASSIGN_610, cmd, to.ID);
                            ValidResultCode(cmd, vh, to);
                            SendVehicleStatusToWeb( vh.ID );
                            this.SendVehicleSubStateToWeb( vh.ID );
                            continue;
                    }

                    this.sql.Unit.ChgSubState(vh.ID, E_VEHICLE_SUB_STATE.NOTASSIGN);
                    SendVehicleStatusToWeb(vh.ID);
                    this.SendVehicleSubStateToWeb(vh.ID);
                    logger.W("[{0}] VEHICLE UNASSIGNED..", vhid);
                    this.EventRpt6(H_CEID.VEHICLE_UNASSIGN_610, cmd, to.ID);

                    //BCR Check
                    {
                        if (to.IsPortReal)
                        {
                            try
                            {
                                logger.W("[{0}] WAIT BCR-CHECK CommandID : {1}, CarrierID : {2}", vhid, cmd.ID, carrier.ID);
                                waked = plc.WaitChgBits(true, WAIT_VRP_VEHICLE, out awoken, to.ID + "#IDREADACK", to.ID + "#IDREADFAIL", vh.ID + ("#ABORT"));
                                Assert.IsTrue(waked, "[{0}] Vrp BCR CHECK timeout", vhid);
                            }
                            catch (AssertException e)
                            {
                                logger.W("[{0}] {1}", vhid, e.Message);
                                TransferComplete(cmd, 21, false, false);
                                this.RemoveCarrier(carrier.ID, to.ID, E_IDREAD_STATUS.FAIL);

                                LockUtils.Wait(300);

                                //UNK Install 처리한다.
                                string unkID = string.Format("UNKNOWN-{0}", DateTime.Now.ToString("yyyyMMddHHmmssff"));
                                this.InstallCarrier(unkID, to.ID, E_IDREAD_STATUS.FAIL);
                                logger.W("[{0}] INSTALL UNKNOWN CARRIER UnitID : {1}, CarrierID : {2}", vhid, to.ID, unkID);

                                //2017.07.13 Toptec Kang.
                                plc.WriteBit( to.ID + "#BCRMISMATCH", true );

                                this.SendUnitStatusToWeb(to.ID);
                                continue;
                            }
                            sql.Command.ChgFinalLoc(cmd.ID, to.ID);

                            switch (awoken.SubNo)
                            {
                                //ACK
                                case 1:
                                    string plcCarrierID = plc.ReadWord(to.ID + SPLIT_PREFIX + "CARRIERID").Value.Trim();//carrierID -> PlcCarrierID를 통해 찾아옴.
                                    string carrierID = sql.Carrier.HasN(x => x.plcCarrier.Equals(plcCarrierID)) ? sql.Carrier.GetCarrierByPlcCarrier(plcCarrierID).ID : plcCarrierID;

                                    logger.W("[{0}] RESULT BCR-READ SUCCESS CommandID : {1}, CarrierID : {2}, PlcCarrierID : {3}", vhid, cmd.ID, carrierID, plcCarrierID);

                                    //CARRIERID 공백이면 UNK CARRIER 생성
                                    if (String.IsNullOrEmpty(carrierID))
                                        carrierID = string.Format("UNKNOWN-{0}", DateTime.Now.ToString("yyyyMMddHHmmssff"));

                                    //Duplicate Carrier
                                    bool isCarrier = sql.Carrier.Has(carrierID);
                                    if (isCarrier)
                                    {
                                        TcsCarrier c = sql.Carrier[carrierID]; 
                                        if (!c.CurrUnitID.Equals(to.ID))
                                        {
                                            var cUnit = sql.Unit[c.CurrUnitID];
                                            if (cUnit.IsPortReal) // Real Port 인경우만 dup check - kbs 06/16
                                            {
                                                TransferComplete(cmd, 22, false, false);
                                                logger.W("[{0}] BCR-CARRIER_DUPLICATE CarrierID : {1}", vhid, carrierID);

                                                //기존위치 자재 삭제
                                                //logger.W("[{0}] BCR-CARRIER_DUPLICATE Carrier Removed UnitID : {1}, CarrierID : {2}", vhid, c.CurrUnitID, c.ID);
                                                //this.EventRpt4(H_CEID.CARRIERREMOVED_302, cmd.Carrier, E_IDREAD_STATUS.DUPLICATE);

                                                LockUtils.Wait(300);

                                                //Dup Carrier 등록
                                                string dupID = string.Format("UNKNOWNDUP-{0}{1}", c.ID, DateTime.Now.ToString("yyyyMMddHHmmssff"));
                                                this.InstallCarrier(dupID, to.ID, E_IDREAD_STATUS.DUPLICATE);

                                                //2017.07.13 Toptec Kang.
                                                plc.WriteBit( to.ID + "#BCRMISMATCH", true );

                                                logger.W("[{0}] BCR-CARRIER_DUPLICATE Carrier Install UnitID : {1}, CarrierID : {2}", vhid, to.ID, dupID);
                                                continue;
                                            }
                                        }
                                    }

                                    if (carrier.ID.Equals(carrierID))
                                    {
                                        sql.Carrier.ChgLocation(carrier.ID, to.ID);
                                        this.SendUnitStatusToWeb(to.ID);
                                    }
                                    //Carrier MissMatch 인 경우, 기존자재 Remove, Missmatch 자재 Install
                                    else
                                    {
                                        //Orignal Carrier 삭제
                                        TransferComplete(cmd, 23, false, false);                                       
                                        logger.W("[{0}] BCR-CARRIER_MISSMATCH TcsCarrierID : {1}, PlcCarrierID : {2}", vhid, carrier.ID, plcCarrierID);
                                        if (sql.Command.Has(cmd.ID)) sql.Command.ChgAssigned(cmd.ID, false);
                                        //Event 없이 자재만 Delete - 6/20 kbs
                                        sql.Carrier.Del(carrier.ID);
                                        logger.W("[{0}] Carrier Delete : {1}, Becuse of Carier Missmatch", vhid, carrier.ID);
                                        //this.RemoveCarrier(carrier.ID, carrier.CurrUnitID, E_IDREAD_STATUS.MISSMATCH);
                                        logger.W("[{0}] BCR-CARRIER_MISSMATCH CARRIER-REMOVED UnitID : {1}, CarrierID : {2}", vhid, carrier.CurrUnitID, carrier.ID);
                                        this.SendUnitStatusToWeb(carrier.CurrUnitID);

                                        LockUtils.Wait(300);
                                        //2017 02 17 by mc
                                        //description - mismatch 인경우 실제 ID read 시 해당 carrier 존재 여부 dup 여부 확인 필요해보임.
                                        //사유        - test 도중 mismatch 인경우 같은 id가 존재 함에도 unit 정보만 바뀌고 dup로 install 되지 않음. 
                                        
                                        //공통BCR Logic 에서 처리
                                        if (sql.Carrier.Has(carrierID))
                                        {
                                            string dupID = string.Format("UNKNOWNDUP-{0}{1}", carrierID, DateTime.Now.ToString("yyyyMMddHHmmssff"));
                                            this.InstallCarrier(dupID, to.ID, E_IDREAD_STATUS.DUPLICATE);
                                            logger.W("[{0}] BCR-CARRIER_DUPLICATE Carrier Install UnitID : {1}, CarrierID : {2}", vhid, to.ID, dupID);
                                        }
                                        else
                                        {
                                            this.InstallCarrier(carrierID, to.ID, E_IDREAD_STATUS.MISSMATCH);
                                            logger.W("[{0}] BCR-CARRIER_MISSMATCH CARRIER-INSTALL UnitID : {1}, CarrierID : {2}", vhid, to.ID, carrierID);
                                        }

                                        //2017.07.13 Toptec Kang.
                                        plc.WriteBit( to.ID + "#BCRMISMATCH", true );

                                        this.SendUnitStatusToWeb(to.ID);
                                        continue;
                                    }

                                    //Unit 에 다른자재(쓰레기자재)가 점유 하고 있었던 경우, OLD CARRIER 자체 삭제.
                                    IList<TcsCarrier> carrierList = this.sql.Carrier.GetForLocation(to.ID);
                                    foreach (var c in carrierList)
                                    {
                                        if (c.ID.Equals(carrierID)) continue;                                        
                                        this.sql.Carrier.Del(c.ID);
                                        this.SendUnitStatusToWeb(c.CurrUnitID);
                                        logger.W("[{0}] BCR-CARRIER_VALIDATE Garbage Carrier REMOVED UnitID : {1}, CarrierID : {2}", vhid, c.CurrUnitID, c.ID);
                                    }
                                    
                                    {
                                        this.InstallCarrier(carrierID, to.ID, E_IDREAD_STATUS.SUCCESS);
                                        this.SendUnitStatusToWeb(to.ID);
                                        logger.W("[{0}] BCR-SUCCESS CARRIER-INSTALL UnitID : {1}, CarrierID : {2}", vhid, to.ID, carrierID);
                                    }

                                    break;
                                //NACK
                                case 2:

                                    TransferComplete(cmd, 21, false, false);

                                    //2017.07.13 Toptec Kang.
                                    plc.WriteBit( to.ID + "#BCRMISMATCH", true );
                                    
                                    //this.RemoveCarrier(carrier.ID, to.ID, E_IDREAD_STATUS.FAIL);
                                    //logger.W("[{0}] RESULT BCR-READ READ-FAIL UnitID : {1}, CIMCarrierID : {2}", vhid, to.ID, carrier.ID);

                                    //LockUtils.Wait(300);

                                    //UNK Install 처리한다.                                   
                                    //string unkID = string.Format("UNKNOWN-{0}", DateTime.Now.ToString("yyyyMMddHHmmssff"));
                                    //this.InstallCarrier(unkID, to.ID, E_IDREAD_STATUS.FAIL);
                                    //this.SendUnitStatusToWeb(to.ID);
                                    //logger.W("[{0}] RESULT BCR-READ READ-FAIL Install UNKNOWNCARRIER : {1}", vhid, unkID);
                                    continue;
                                //ABORT
                                case 6:
                                    //2017.07.13 Toptec Kang.
                                    plc.WriteBit( to.ID + "#BCRMISMATCH", true );

                                    logger.W("[{0}] RESULT BCR READ ABORT CommandID : {1}", vhid, cmd.ID);
                                    this.PlcAbort(cmd, false, 21);

                                    continue;
                                default:
                                    //2017.07.13 Toptec Kang.
                                    plc.WriteBit( to.ID + "#BCRMISMATCH", true );

                                    logger.W("[{0}] RESULT BCR READ UNKNOWN ERROR CommandID : {1}", vhid, cmd.ID);
                                    this.PlcAbort(cmd, false, 21);
                                    continue;
                            }
                        }
                    }
                    #endregion

                    if (sql.Command.Has(cmd.ID))
                    {
                        if (sql.SubCmd.HasN(x => x.CmdID == cmd.ID))
                        {
                            logger.W("[{0}] Delete SubCommand {0}", vhid, sql.SubCmd[vh.ID].CmdID);
                            sql.SubCmd.Del(vh.ID);
                        }

                        //Transfer comp
                        if (to.ID.Equals(cmd.DestUnitID))
                        {
                            logger.W("[{0}] TRANSFER COMPLETE Source : {1}, Dest : {2}", vhid, cmd.SrcUnitID, cmd.DestUnitID);
                            TransferComplete(cmd, 0, false, false);
                        }

                        sql.Command.ChgAssigned( cmd.ID, false );
                        logger.W( "[{0}] CHANGE COMMAND ASSIGNED CommandID : {1}", vhid, cmd.ID );
                    }
                }
                catch (Exception e)
                {
                    logger.E("[{0}] {1}", vhid, e.Message);
                }
                finally
                {
                    LockUtils.Wait(WAIT_VRP);
                }
            }
        }

        private void ValidAbortCode(TcsCommand cmd, TcsUnit vh, TcsUnit unit, bool v)
        {
            if (vh.UnitType == E_UNIT_TYPE.MCT)
            {
                if (cmd.SrcUnitID == unit.ID) this.PlcAbort(cmd, v, 35);
                else if (cmd.DestUnitID.Equals(unit.ID)) this.PlcAbort(cmd, v, 36);
                else this.PlcAbort(cmd, v, 35);
            }
            else if (vh.UnitType == E_UNIT_TYPE.FTE)
            {
                if (cmd.SrcUnitID == unit.ID) this.PlcAbort(cmd, v, 5);
                else if (cmd.DestUnitID.Equals(unit.ID)) this.PlcAbort(cmd, v, 6);
                else this.PlcAbort(cmd, v, 5);
            }
            else
            {
                logger.E("[{0}] {1}", vh.ID, "Vehicle Type Error - TrasferComplete Not execute");
            }
        }

        private void ValidResultCode(TcsCommand cmd, TcsUnit vh, TcsUnit unit)
        {
            if(vh.UnitType == E_UNIT_TYPE.MCT)
            {
                if(cmd.SrcUnitID == unit.ID) this.TransferComplete(cmd, 35, true, false);
                else if(cmd.DestUnitID.Equals(unit.ID)) this.TransferComplete(cmd, 36, false, true);
                else this.TransferComplete(cmd, 36, true, false);
            }
            else if(vh.UnitType == E_UNIT_TYPE.FTE)
            {
                if (cmd.SrcUnitID == unit.ID) this.TransferComplete(cmd, 5, true, false);
                else if (cmd.DestUnitID.Equals(unit.ID)) this.TransferComplete(cmd, 6, false, true);
                else this.TransferComplete(cmd, 6, false, true);
            }
            else
            {
                logger.E("[{0}] {1}", vh.ID, "Vehicle Type Error - TrasferComplete Not execute");
            }
        }
    }
}

