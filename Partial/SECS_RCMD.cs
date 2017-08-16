using System;
using System.Collections.Generic;
using Floware.SECSFlow.Core.Object;
using TcsV2.Core;
using TcsV2.Core.MODEL;
using Floware.Utils;

namespace TCS.ServerV2
{
    partial class FormMain
    {
        /// <summary>
        /// S6F11
        /// RPT = 2
        /// </summary>
        /// <param name="recd"></param>
        void HandleCmdAbortCancel(SFMessage recd)
        {
            string rcmd = recd["RCMD"].Value.Trim();
            string cmdID = recd["CommandID"].Value.Trim();

            //반송 명령 체크
            bool hasCmd = this.sql.Command.Has(cmdID);
            if (!hasCmd)
            {
                logger.W("CommandID does not exist. ID=[{0}]", cmdID);
                ReplyS2F42(recd, E_HCACK.CMD_NOT_EXIST, cmdID, E_CPACK.CPNAME_NOT_EXIST);

                AddNakHis(rcmd, cmdID, E_HCACK.CMD_NOT_EXIST, "CommandID Error");
                return;
            }
            ReplyS2F42(recd, E_HCACK.CONFIRMED, "", E_CPACK.NO_ERROR);        
            TcsCommand cmd = this.sql.Command[cmdID];
            if (rcmd.Equals("ABORT"))
            {
                this.HostAbort(cmd, false, string.Empty, string.Empty);
            }
            else if (rcmd.Equals("CANCEL"))
            {
                this.HostCancel(cmd, false, string.Empty, string.Empty);
            }
        }

        /// <summary>
        /// S6F11
        /// RPT = 2
        /// </summary>
        /// <param name="recd"></param>
        void HandleCmdPauseResume(SFMessage recd)
        {
            string rcmd = recd["RCMD"].Value.Trim();            
            switch(rcmd)
            {
                case "PAUSE":
                    sql.HsmsConfig.ChgControlState(E_CTL_STATE.PAUSED);
                    ReplyS2F42(recd, E_HCACK.CONFIRMED, "", E_CPACK.NO_ERROR);
                    EventRpt1(H_CEID.PAUSEINIT_107);
                    EventRpt1(H_CEID.PAUSECOMPLE_105);
                    SendStateToWeb();

                    break;

                case "RESUME":
                    sql.HsmsConfig.ChgControlState(E_CTL_STATE.AUTO);
                    ReplyS2F42(recd, E_HCACK.CONFIRMED, "", E_CPACK.NO_ERROR);
                    EventRpt1(H_CEID.AUTOCOMPLETE_103);
                    SendStateToWeb();
                    break;
            }
        }
        /// <summary>
        /// S6F11
        /// RPT = 2
        /// </summary>
        /// <param name="recd"></param>
        void HandleCmdUpdate(SFMessage recd)
        {
            string rcmd = recd["RCMD"].Value.Trim();
            string cmdID = recd["CommandID"].Value.Trim();
            string chgPort = recd["DestPort"].Value.Trim();

            //반송 명령 체크
            bool hasCmd = this.sql.Command.Has(cmdID);
            if (!hasCmd)
            {
                logger.W("CommandID does not exist. ID=[{0}]", cmdID);
                ReplyS2F42(recd, E_HCACK.CMD_NOT_EXIST, cmdID, E_CPACK.CPNAME_NOT_EXIST);

                AddNakHis(rcmd, cmdID, E_HCACK.CMD_NOT_EXIST, "CommandID Error");
                return;
            }

            bool hasSrc = sql.Unit.Has(chgPort);
            if(!hasSrc)
            {
                logger.W("Change port not exist. ID=[{0}]", chgPort);
                ReplyS2F42(recd, E_HCACK.OBJECT_NOT_EXIST, chgPort, E_CPACK.CPNAME_NOT_EXIST);
                return;
            }

            TcsCommand cmd = this.sql.Command[cmdID];
            {
                TcsCommand dto = cmd;
                dto.DestUnitID = chgPort;

                int ack = this.HostDestChange(dto);

                ReplyS2F42(recd, EnumUtils.Chg<E_HCACK>(ack), cmd.VehicleID, E_CPACK.CPNAME_NOT_VAILD);
                if(ack == 0)
                {
                    EventRpt3(H_CEID.TRANSFER_UPDATE_COMP_212, sql.Command[cmd.ID]);
                }
                else
                {
                    EventRpt3(H_CEID.TRANSFER_UPDATE_FAIL_213, sql.Command[cmd.ID]);
                }
            }
        }

        void HandleCarrierRemove(SFMessage recd)
        {
            var carID = recd["CarrierID"].Value.Trim();
            bool has = sql.Carrier.Has(carID);

            if(has)
            {
                ReplyS2F42(recd, E_HCACK.CONFIRMED, "", E_CPACK.NO_ERROR);
            }

            sql.Carrier.Del(carID);

            //carrier removed
            {
                var c = sql.Carrier[carID];
                var v = mgr["ERS-RPT22"];
                v["CEID"].IntValue = (int)H_CEID.CARRIERREMOVED_302;
                v["EqpName"].Value = sql.HsmsConfig.EqpName.Value;

                v["CommandID"].Value = c.CommandID;
                v["CarrierID"].Value = c.ID;
                v["TransferPort"].Value = string.Empty;
                v["CarrierLoc"].Value = c.CurrUnitID;
                SendEvent(v, H_CEID.CARRIERREMOVED_302);
            }
        }
        /// <summary>
        /// Host Command Parameter Acknowledge Code 
        /// 0 = Confirmed, the command was executed(The transport system doesn't use this                    
        /// value.Confirmation is made using the number 4 value.) 
        /// 1 = Command does not exist
        /// 2 = Currently not able to execute 
        /// 3 = At least one parameter is invalid, Parameter Update
        /// 4 = Confirmed, the command will be executed and completion will be notified by an event
        /// 5 = Rejected, Already requested 
        /// 6 = Object doesn't exist 
        /// </summary>
        /// <param name="recd"></param>
        /// <param name="hAck"></param>
        void ReplyS2F42(SFMessage recd,E_HCACK hAck, string val, E_CPACK cpAck)
        {
            switch (EnumUtils.IntV(hAck))
            {
                case 0:
                case 3:
                    {
                        SFMessage rep = mgr["HCA"];
                        rep.Systembyte = recd.Systembyte;
                        rep["HCACK"].IntValue = EnumUtils.IntV(hAck);
                        ReplyHsms(rep);
                        break;
                    }
                default:
                    {
                        SFMessage rep = mgr["HCA_1"];
                        rep.Systembyte = recd.Systembyte;
                        rep["HCACK"].IntValue = EnumUtils.IntV(hAck);
                        rep["CPNAME"].Value = val;
                        rep["CPACK"].IntValue = EnumUtils.IntV(cpAck);

                        ReplyHsms(rep);
                        break;

                    }
            }
        }

        /// <summary>
        /// <para>■ 0 = Confirmed, the command was executed</para>
        /// <para>(The transfer system doesn't use this value, Confirmation is made using the number 4 value)</para>
        /// <para>■ 1 = Command does not exist</para>
        /// <para>■ 2 = Currently not able to execute</para>
        /// <para>■ 3 = At least one parameter is invalid</para>
        /// <para>■ 4 = Confirmed, the command will be executed and completion will be notified by an event</para>
        /// <para>■ 5 = Rejected, Already requested</para>
        /// <para>■ 6 = Object doesn't exist</para>
        /// <para>■ 10 = Process type mismatch</para>
        /// </summary>
        void ReplyS2F50(SFMessage recd, E_HCACK hAck, Dictionary<string, E_CPACK> cpList)
        {
            string cmdID = recd["CommandID"].Value.Trim();

            int count = cpList.Count;

            SFMessage rep = count > 0 ? mgr["ERCA_1"] : mgr["ERCA"]; //S2F50
            rep.Systembyte = recd.Systembyte;
            rep["HCACK"].IntValue = EnumUtils.IntV(hAck);

            if (count > 0)
            {
                rep["ERRCODE"].DuplicateChildren(count);

                int i = 0;
                foreach (KeyValuePair<string, E_CPACK> kvp in cpList)
                {
                    rep["CPNAME" + i].Value = kvp.Key;
                    rep["CPACK" + i].IntValue = EnumUtils.IntV(kvp.Value);

                    i++;
                }
            }

            ReplyHsms(rep);

            //NAK 히스토리 저장
            if (EnumUtils.IntV(hAck) == 2)//cannot perform now(2), cst size ng(67)
            {
                string cmdid = recd["CommandID"].Value.Trim();
                string carid = recd["CarrierID"].Value.Trim();
                string msg = string.Empty;
                
                msg = string.Format("[{0}] Cmd:{1} Car:{2} Err:{3}", recd.SxFx, cmdid, carid, "Vehicle or Unit State is not Normal");

                this.sql.HostNak.AddNak(EnumUtils.IntV(hAck), cmdID, msg);
            }
            else if (hAck > 0 || cpList.Count > 0)
            {
                string cmdid = recd["CommandID"].Value.Trim();
                string carid = recd["CarrierID"].Value.Trim();
                string errMsg = string.Empty;
                foreach (KeyValuePair<string, E_CPACK> kvp in cpList)
                {
                    errMsg += string.Format("[{0}] Cmd:{1} Car:{2} Err:{3}", recd.SxFx, cmdid, carid,  kvp.Key);
                }

                string msg = string.Format("[{0}] {1} ", recd.SxFx, errMsg);
                this.sql.HostNak.AddNak(EnumUtils.IntV(hAck), cmdID, msg);
            }

            if (hAck > 0)
            {
                //Command History에도 저장함~~. S2F49일때만 저장함
                TcsCommand cmd = new TcsCommand();
                cmd.ID = recd["CommandID"].Value.Trim();
                cmd.CarrierID = recd["CarrierID"].Value.Trim();
                cmd.Priority = recd["Priority"].IntValue;
                cmd.MachineID = sql.HsmsConfig.EqpName.Value;
                cmd.SrcUnitID = recd["SourcePort"].Value.Trim();
                cmd.DestUnitID = recd["DestPort"].Value.Trim();
                cmd.FinalLocation = cmd.Carrier == null ? recd["SourcePort"].Value.Trim() : cmd.Carrier.CurrUnitID;
                cmd.State = E_CMD_STATE.FAIL;
                this.sql.HisCommand.AddHis(cmd);
            }
        }

        void AddNakHis(string rcmd, string cmdID, E_HCACK code, string text)
        {
            string msg = string.Format("[{0}] {1}", rcmd, text);
            this.sql.HostNak.AddNak(EnumUtils.IntV(code), cmdID, msg);
        }
    }
}
