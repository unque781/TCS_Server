using System;
using Floware.SECSFlow.Core.Object;
using Floware.Utils;
using TcsV2.Core;
using TcsV2.Core.MODEL;
using System.Linq;
using Floware.MQ;

namespace TCS.ServerV2
{
    partial class FormMain
    {
        private string GetCntName()
        {
            return sql.HsmsConfig.EqpName.Value;
        }
        private void SendEvent(SFMessage req, H_CEID ceid)
        {
            ChgEventName(req, ceid);
            RequestHsms(req);
        }
        /// <summary>
        /// CEID = 103 : AUTOCOMPLETE
        /// CEID = 105 : PAUSECOMPLETE
        /// CEID = 107 : PAUSEINIT
        /// </summary>
        /// <param name="ceid"></param>
        /// <param name="dto"></param>
        private void EventRpt1(H_CEID ceid)
        {
            SFMessage req = mgr["ERS-RPT1"];
            req["CEID"].EnumValue = ceid;
            req["EqpName"].Value = GetCntName();

            SendEvent(req, ceid);
        }
        /// <summary>
        /// CEID = 101 : AlarmCleared
        /// CEID = 102 : AlarmSet
        /// </summary>
        /// <param name="ceid"></param>
        /// <param name="dto"></param>
        private void EventRpt2(H_CEID ceid, TcsUnit v)
        {
            SFMessage req = mgr["ERS-RPT2"];
            req["CEID"].EnumValue = ceid;
            req["EqpName"].Value = v.MachineID;
            req["CommandID"].Value = string.Empty;
            req["VehicleID"].Value = v.ID;
            req["VehicleState"].EnumValue = (v.State);

            SendEvent(req, ceid);
        }

        /// <summary>
        /// CEID = 201 : TransferAbortCompleted
        /// CEID = 202 : TransferAbortFailed
        /// CEID = 203 : TransferAbortInitiated
        /// CEID = 204 : TransferCancelCompleted
        /// CEID = 205 : TransferCancelfailed
        /// CEID = 206 : TransferCancelInitiated
        /// CEID = 207 : TransferCompleted
        /// CEID = 208 : TransferInitiated
        /// CEID = 209 : TransferPaused
        /// CEID = 210 : TransferResumed
        /// CEID = 211 : Transferring
        /// CEID = 212 : TransferUpdateCompleted
        /// CEID = 213 : TransferUpdateFailed
        /// </summary>
        /// <param name="ceid"></param>
        /// <param name="dto"></param>
        private void EventRpt3(H_CEID ceid, TcsCommand cmd)
        {
            SFMessage req = mgr["ERS-RPT3"];
            req["CEID"].EnumValue = ceid;
            req["EqpName"].Value = cmd.MachineID;
            req["CommandID"].Value = cmd.ID;
            req["CarrierID"].Value = cmd.CarrierID;
            req["SourcePort"].Value = HostID(cmd.SrcUnitID);
            req["DestPort"].Value = HostID(cmd.DestUnitID);
            req["CarrierCleanStatus"].EnumValue = cmd == null || cmd.Carrier == null ? TcsCarrier.CLEAN_STATE.DEFAULT : cmd.Carrier.CleanState;
            req["CarrierLoc"].Value = cmd.Carrier == null ? sql.Carrier.ListN(x => x.ID.Equals(cmd.CarrierID)).Select(x => x.CurrUnitID).First() : cmd.Carrier.CurrUnitID;
            req["ResultCode"].IntValue = cmd.ResultCode;

            SendEvent(req, ceid);
        }

        private string HostID(string unit)
        {
            if (!sql.Unit.Has(unit)) return "";
            return sql.Unit[unit].HostID;
        }


        /// <summary>
        /// CEID = 301 : CarrierInstalled
        /// CEID = 302 : CarrierRemoved
        /// </summary>
        /// <param name="ceid"></param>
        /// <param name="dto"></param>
        private void EventRpt4(H_CEID ceid, TcsCarrier cd, E_IDREAD_STATUS status)
        {
            var ud = sql.Unit[cd.CurrUnitID];
            var cmd = sql.Command[cd.CommandID];

            SFMessage req = mgr["ERS-RPT4"];
            req["CEID"].EnumValue = ceid;
            req["EqpName"].Value = ud.MachineID;
            req["VehicleID"].Value = ud.IsVehicle ? ud.HostID : string.Empty;
            req["CarrierID"].Value = cd.ID;
            req["CarrierLoc"].Value = ud.HostID;
            req["CommandID"].Value = null == cmd ? string.Empty : cmd.ID;
            req["IDReadStatus"].EnumValue = (status);
            req["CarrierCleanStatus"].EnumValue = cd.CleanState;// c.Carrier.CleanState;
            
            SendEvent(req, ceid);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ceid"></param>
        /// <param name="dto"></param>
        private void EventRpt5(H_CEID ceid, TcsCommand cmd)
        {
            SFMessage req = mgr["ERS-RPT5"];
            req["CEID"].EnumValue = ceid;
            req["PortID"].Value = "";

            SendEvent(req, ceid);
        }

        /// <summary>
        /// CEID = 303 : CarrierPreHandoff
        /// CEID = 601 : VehicleArrived
        /// CEID = 602 : VehicleAcquireStarted
        /// CEID = 603 : VehicleAcquireCompleted
        /// CEID = 604 : VehicleAssigned
        /// CEID = 605 : VehicleDeparted
        /// CEID = 606 : VehicleDepositStarted
        /// CEID = 607 : VehicleDepositCompleted
        /// CEID = 608 : VehicleInstalled
        /// CEID = 609 : VehicleRemoved
        /// CEID = 610 : VehicleUnassigned
        /// </summary>
        /// <param name="ceid"></param>
        /// <param name="dto"></param>
        private void EventRpt6(H_CEID ceid, TcsCommand cmd, string Port)
        {
            TcsCommand nCmd = sql.Command[cmd.ID];

            SFMessage req = mgr["ERS-RPT6"];
            req["CEID"].EnumValue = ceid;
            req["EqpName"].Value = nCmd.MachineID;
            req["VehicleID"].Value = HostID(nCmd.VehicleID);
            req["CommandID"].Value = nCmd.ID;
            req["TransferPort"].Value = Port;
            req["CarrierID"].Value = nCmd.CarrierID;

            SendEvent(req, ceid);
        }

        private void EventRpt6(H_CEID ceid, TcsUnit v, E_VEHICLE_INST_STATE actionType)
        {
            TcsCommand cmd = new TcsCommand();

            if (sql.Command.HasN(x=>x.VehicleID == v.ID)) cmd = sql.Command.GetCommandIDByVehicle(v.ID);

            SFMessage req = mgr["ERS-RPT6"];
            req["CEID"].EnumValue = ceid;
            req["EqpName"].Value = v.MachineID;
            req["VehicleID"].Value = v.ID;
            req["CommandID"].Value = cmd == null ? "" : cmd.ID;
            req["TransferPort"].Value = cmd == null ? "" : cmd.SrcUnitID;
            req["CarrierID"].Value = cmd == null ? "" : cmd.CarrierID;

            SendEvent(req, ceid);
        }

        /// <summary>
        /// CEID = 701 : UnitAlarmCleared
        /// CEID = 702 : UnitAlarmSet
        /// </summary>
        /// <param name="ceid"></param>
        /// <param name="dto"></param>
        private void EventRpt7(H_CEID ceid, TcsAlarm alarm)
        {
            //string m = "";
            //if (sql.Unit.HasN(x => x.ID == alarm.UnitID)) m = sql.Unit[alarm.UnitID].MachineID;
            SFMessage req = mgr["ERS-RPT7"];
            req["CEID"].EnumValue = ceid;
            req["EqpName"].Value = alarm.MachineID;
            req["UnitID"].Value = alarm.UnitID;
            req["AlarmID"].IntValue = alarm.AlarmNo;
            req["AlarmText"].Value = alarm.AlarmText;

            SendEvent(req, ceid);
        }

        /// <summary>
        /// CEID = 611 : VehicleStatusChanged
        /// </summary>
        /// <param name="ceid"></param>
        /// <param name="dto"></param>
        private void EventRpt8(H_CEID ceid, TcsUnit v,E_VEHICLE_STATUS status)
        {
            SFMessage req = mgr["ERS-RPT8"];
            req["CEID"].EnumValue = ceid;
            req["EqpName"].Value = v.MachineID;
            req["VehicleID"].Value = v.ID;
            req["VehicleStatus"].IntValue = EnumUtils.IntV(status);

            SendEvent(req, ceid);
        }

        /// <summary>
        /// CEID = 501 : OperatorInitiatedAction
        /// </summary>
        /// <param name="ceid"></param>
        /// <param name="dto"></param>
        private void EventRpt9(H_CEID ceid, TcsCommand cmd, H_OPER_INIT abort)
        {
            TcsCommand nCmd = sql.Command[cmd.ID];

            SFMessage req = mgr["ERS-RPT9"];
            req["CEID"].EnumValue = ceid;
            req["EqpName"].Value = cmd.MachineID;
            req["CommandID"].Value = nCmd.ID;
            req["CommandType"].Value = abort.ToString();
            req["CarrierID"].Value = nCmd.CarrierID;
            req["SourcePort"].Value = nCmd.SrcUnitID;
            req["DestPort"].Value = nCmd.DestUnitID;
            req["Priority"].IntValue = nCmd.Priority;
            req["CarrierCleanStatus"].EnumValue = nCmd.Carrier.CleanState;

            SendEvent(req, ceid);
        }
        private void EventRpt10(H_CEID ceid, TcsUnit unit, int state)
        {
            //state 1= inservice , 2 = outservice
            SFMessage req = mgr["ERS-RPT10"];
            req["CEID"].EnumValue = ceid;
            req["EqpName"].Value = unit.MachineID;
            req["UnitName"].Value = unit.ID;
            req["UnitState"].IntValue = state;

            SendEvent(req, ceid);
        }

        private void ChgEventName(SFMessage req, H_CEID ceid)
        {
            switch (ceid)
            {
                case H_CEID.OFFLINE_1: //RPT 1
                    req.Name = "EquipmentOffline";
                    break;

                case H_CEID.ALARMCLEARED_101:
                    req.Name = "AlarmCleared";
                    break;

                case H_CEID.ALARMSET_102:
                    req.Name = "AlarmSet";
                    break;

                case H_CEID.AUTOCOMPLETE_103:
                    req.Name = "AutoCompleted";
                    break;

                case H_CEID.AUTOINIT_104:
                    req.Name = "AutoInitiated";
                    break;

                case H_CEID.PAUSECOMPLE_105:
                    req.Name = "PauseCompleted";
                    break;

                case H_CEID.PAUSED_106:
                    req.Name = "Paused";
                    break;

                case H_CEID.PAUSEINIT_107:
                    req.Name = "PauseInitiated";
                    break;

                case H_CEID.TRANSFER_COMP_207: //RPT 2
                    req.Name = "TransferCompleted";
                    break;

                case H_CEID.TRANSFER_PAUSED_209:
                    req.Name = "TransferPaused";
                    break;

                case H_CEID.TRANSFER_ABORT_COMP_201: //RPT 3
                    req.Name = "TransferAbortCompleted";
                    break;

                case H_CEID.TRANSFER_ABORT_FAIL_202:
                    req.Name = "TransferAbortFailed";
                    break;

                case H_CEID.TRANSFER_ABORT_INIT_203:
                    req.Name = "TransferAbortInitiated";
                    break;

                case H_CEID.TRANSFER_CANCEL_COMP_204:
                    req.Name = "TransferCancelCompleted";
                    break;

                case H_CEID.TRANSFER_CANCEL_FAIL_205 :
                    req.Name = "TransferCancelfailed";
                    break;

                case H_CEID.TRANSFER_CANCEL_INIT_206:
                    req.Name = "TransferCancelInitiated";
                    break;

                case H_CEID.TRANSFER_INIT_208:
                    req.Name = "TransferInitiated";
                    break;

                case H_CEID.TRANSFER_RESUMED_210:
                    req.Name = "TransferResumed";
                    break;

                case H_CEID.TRANSFERRING_211:
                    req.Name = "Transferring";
                    break;

                case H_CEID.TRANSFER_UPDATE_FAIL_213:
                    req.Name = "TransferUpdateFailed";
                    break;

                case H_CEID.TRANSFER_UPDATE_COMP_212:
                    req.Name = "DestUpdateCompleted";
                    break;

                case H_CEID.CARRIERINSTALL_301:
                    req.Name = "CarrierInstalled";
                    break;

                case H_CEID.CARRIERREMOVED_302:
                    req.Name = "CarrierRemoved";
                    break;

                case H_CEID.VEHICLE_ARRIVED_601:
                    req.Name = "VehicleArrived";
                    break;

                case H_CEID.VEHICLE_DEPART_605:
                    req.Name = "VehicleDeparted";
                    break;

                case H_CEID.VEHICLE_ACQ_START_602:
                    req.Name = "VehicleAcquireStarted";
                    break;

                case H_CEID.VEHICLE_ACQ_COMP_603:
                    req.Name = "VehicleAcquireCompleted";
                    break;

                case H_CEID.VEHICLE_DEP_START_606:
                    req.Name = "VehicleDepositStarted";
                    break;

                case H_CEID.VEHICLE_DEP_COMP_607:
                    req.Name = "VehicleDepositCompleted";
                    break;

                case H_CEID.CARRIER_PREHANDOFF_303:
                    req.Name = "CarrierPreHandoff";
                    break;

                case H_CEID.VEHICLE_ASSIGN_604:
                    req.Name = "VehicleAssigned";
                    break;

                case H_CEID.VEHICLE_UNASSIGN_610:
                    req.Name = "VehicleUnassigned";
                    break;

                case H_CEID.VEHICLE_INSTALL_608:
                    req.Name = "VehicleInstalled";
                    break;

                case H_CEID.VEHICLE_REMOVE_609:
                    req.Name = "VehicleRemoved";
                    break;

                case H_CEID.OPERATOR_INIT_ACTION_501:
                    req.Name = "OperatorInitiatedAction";
                    break;

                case H_CEID.UNIT_ALARMCLEAR_701: //RPT 13
                    req.Name = "UnitAlarmCleared";
                    break;

                case H_CEID.UNIT_ALARMSET_702:
                    req.Name = "UnitAlarmSet";
                    break;

                case H_CEID.MACHINE_OUTSERVICE_503:
                    req.Name = "MachineOutSerivce";
                    break;

                case H_CEID.MACHINE_INSERVICE_502:
                    req.Name = "MachineInService";
                    break;

            }
        }
        private void AlarmReport(MQMessage orgMsg)
        {
            string aID = orgMsg.GetFieldV("ALARMID");
            string u = orgMsg.GetFieldV("UNITID");
            int type = orgMsg.GetFieldIntV("TYPE");
            string alarmText = orgMsg.GetFieldV("ALARMTEXT");
            string machineID = orgMsg.HasField("MACHINEID") ? orgMsg.GetFieldV("MACHINEID") : string.Empty;
            int alarmNo = orgMsg.GetFieldIntV("ALARMNO");
            int alarmCode = orgMsg.GetFieldIntV("ALARMCODE");

            TcsUnit v = sql.Unit[u];

            SFMessage req = mgr["ARS"];
            req["ALCD"].IntValue = alarmCode;
            req["ALID"].IntValue = alarmNo;
            req["ALTX"].Value = alarmText;

                if (type == 1)
                {
                    TcsAlarm alarm = sql.Alarm[aID];
                    RequestHsms(req);
                    EventRpt2(H_CEID.ALARMSET_102, v);
                    EventRpt7(H_CEID.UNIT_ALARMSET_702, alarm);
                }
                else
                {
                    TcsAlarm alarm = new TcsAlarm { UnitID = u, AlarmText = alarmText, Code = alarmCode, AlarmNo = alarmNo };
                    RequestHsms(req);
                    EventRpt2(H_CEID.ALARMCLEARED_101, v);
                    EventRpt7(H_CEID.UNIT_ALARMCLEAR_701, alarm);
                }

            if ( !v.IsVehicle || v.SubState == E_VEHICLE_SUB_STATE.NOTASSIGN )
                return;

            var cmd = sql.Command.GetCommandIDByVehicle( v.ID );
            if ( cmd == null ) return;

            if ( type == 1 )
            {
                sql.Command.ChgState( cmd.ID, E_CMD_STATE.PAUSED );
                EventRpt3( H_CEID.TRANSFER_PAUSED_209, cmd );
            }
            else
            {
                sql.Command.ChgState( cmd.ID, E_CMD_STATE.RESUM );
                EventRpt3( H_CEID.TRANSFER_RESUMED_210, cmd );
            }

            #region old code...
            //if ( v.IsVehicle && v.SubState != E_VEHICLE_SUB_STATE.NOTASSIGN )
            //{
            //    var cmd = sql.Command.GetCommandIDByVehicle( v.ID );
            //    if ( type == 1 )
            //    {
            //        TcsAlarm alarm = sql.Alarm[aID];
            //        RequestHsms( req );
            //        EventRpt2( H_CEID.ALARMSET_102, v );
            //        EventRpt7( H_CEID.UNIT_ALARMSET_702, alarm );
            //        sql.Command.ChgState( cmd.ID, E_CMD_STATE.PAUSED );
            //        EventRpt3( H_CEID.TRANSFER_PAUSED_209, cmd );
            //    }
            //    else
            //    {
            //        TcsAlarm alarm = new TcsAlarm { UnitID = u, AlarmText = alarmText, Code = alarmCode, AlarmNo = alarmNo, MachineID = machineID };
            //        RequestHsms( req );
            //        EventRpt2( H_CEID.ALARMCLEARED_101, v );
            //        EventRpt7( H_CEID.UNIT_ALARMCLEAR_701, alarm );
            //        sql.Command.ChgState( cmd.ID, E_CMD_STATE.RESUM );
            //        EventRpt3( H_CEID.TRANSFER_RESUMED_210, cmd );
            //    }
            //}
            //else
            //{
            //    if ( type == 1 )
            //    {
            //        TcsAlarm alarm = sql.Alarm[aID];
            //        RequestHsms( req );
            //        EventRpt2( H_CEID.ALARMSET_102, v );
            //        EventRpt7( H_CEID.UNIT_ALARMSET_702, alarm );
            //    }
            //    else
            //    {
            //        TcsAlarm alarm = new TcsAlarm { UnitID = u, AlarmText = alarmText, Code = alarmCode, AlarmNo = alarmNo };
            //        RequestHsms( req );
            //        EventRpt2( H_CEID.ALARMCLEARED_101, v );
            //        EventRpt7( H_CEID.UNIT_ALARMCLEAR_701, alarm );
            //    }
            //}
            #endregion
        }
    }
}
