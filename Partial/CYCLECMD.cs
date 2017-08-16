using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Floware.Concurrent;
using Floware.MQ;
using Floware.Utils;
using TcsV2.Core;
using TcsV2.Core.DTO;
using TcsV2.Core.MODEL;
using Floware.V2;
using Floware.LINQ;


namespace TCS.ServerV2
{

    partial class FormMain
    {

        void InitCycleCmd()
        {
            for (; ; )
            {
                try
                {

                    var dd = sql.CycleConfig.All
                        .Where(x => x.Count > 0)
                        .ToDictionary(x => x.ID);

                    sql.Machine.All
                        //.Where(x => !x.NotUse)
                        .Where(x => x.ConnState == E_CONN_STATE.CONNECT)
                        .Where(x => x.State == E_MACHINE_STATE.AUTO)
                        .Where(x => dd.ContainsKey(x.ID))
                        .Where(x => x.FTE.State == E_STATE.RUN)
                        .Where(x => x.MCT.State == E_STATE.RUN)
                        //.Where(x => x.FTE.State == E_STATE.NORMAL)
                        //.Where(x => x.MCT.State == E_STATE.NORMAL)
                        .Where(x => !sql.Command.HasMachine(x.ID))
                        .ToList()
                        .FwEach(x=> MakeCC(x, dd[x.ID]))
                        .FwEach(x => sql.CycleConfig.ChgCycleCountMinus(x.ID))
                        ;

                    
                }
                catch (Exception e)
                {
                    logger.E("[CYCLE] " + e.Message);
                }
                finally
                {
                    LockUtils.Wait(5000);
                }
            }
        }

        void MakeCC(TcsMachine m, TcsCycleConfig cycle)
        {
            LockUtils.Wait(3000);

            var socUnit = m.UnitList.Where(x => x.ID.Equals(cycle.FromUnitID)).First();
            var destUnit = m.UnitList.Where(x => x.ID.Equals(cycle.ToUnitID)).First();
            TcsUnit tUnit = null;
            
            if (cycle.Flag)
            {
                tUnit = socUnit;
                socUnit = destUnit;
                destUnit = tUnit;
            }
            {
                if(!sql.Carrier.HasByUnit(socUnit.ID))
                {
                    logger.I("[CYCLE] socUnit is Empty");
                    return;
                }
                //var nCarrier = new TcsCarrier
                //{
                //    ID = cycle.CarrierID,
                //    AddTime = cycle.AddTime,
                //    CleanState = TcsCarrier.CLEAN_STATE.DEFAULT,
                //    CommandID = string.Empty,
                //    CurrUnitID = socUnit.ID
                //};

                //var cCarrier = sql.Carrier.GetCarrierByUnit(socUnit.ID);

                //if (cCarrier == null)
                //{
                //    sql.Carrier.AddN(nCarrier);
                //}
                //else
                //{
                    //if (!cCarrier.ID.Equals(nCarrier.ID))
                    //{
                    //    logger.I("[CYCLE] Cycle Carrier Vailidation Fail... Delete Loc {0}, Carrier {1}, Create Carrier {2}",
                    //        socUnit.ID, cCarrier.ID, nCarrier.ID);
                    //    sql.Carrier.Del(cCarrier.ID);
                    //    sql.Carrier.AddN(nCarrier);
                    //}
                //}
                
                //if (socUnit.Status == E_UNIT_STATUS.EMPTY)
                //{
                //    logger.I("[CYCLE] Cycle Command Port Status Vailidation Fail... Chg Source Port {0} Status Full", socUnit.ID);

                //    sql.Unit.ChgStatus(socUnit.ID, E_UNIT_STATUS.FULL);
                //    if(socUnit.IsPortReal) plc.WriteWord(string.Format("{0}#STATUS", socUnit.ID), 2);
                //    this.SendUnitStatusToWeb(socUnit.ID);
                //}
            }

            {
                var cmd = new TcsCommand { Priority = 1, MachineID = m.ID, };
                cmd.ID = string.Format("CYC_{0}{1}", socUnit.MachineID, DateTime.Now.ToString("yyyyMMddHHmmssfff"));
                cmd.SrcUnitID = socUnit.ID;
                cmd.DestUnitID = cmd.FinalLocation = destUnit.ID;
                cmd.SrcPointID = socUnit.StationID.ToString();
                cmd.DestPointID = destUnit.StationID.ToString();
                cmd.CarrierID = cycle.CarrierID;
                cmd.State = E_CMD_STATE.NEW;
                cmd.AddTime = DateTime.Now;

                logger.I("[CYCLE] Cycle Transfer Create New Command : {0} ", cmd.ID);

                sql.Command.AddN(cmd);
            }

            if (cycle.Flag)
            {
                cycle.Flag = false;
                sql.CycleConfig.ChgFlag(m.ID, false);
                return;
            }

            cycle.Flag = true;
            sql.CycleConfig.ChgFlag(m.ID, true);
        }

        void MakeCC(TcsCommand cmd)
        {
            cmd.Priority = 1;

            TcsUnit socUnit = sql.Unit[cmd.SrcUnitID];
            TcsUnit detUnit = sql.Unit[cmd.DestUnitID];

            cmd.ID = string.Format("CYC_{0}{1}", socUnit.MachineID, DateTime.Now.ToString("yyyyMMddHHmmssfff"));
            cmd.MachineID = socUnit.MachineID;
            cmd.SrcPointID = socUnit.StationID.ToString();
            cmd.DestPointID = detUnit.StationID.ToString();
            cmd.State = E_CMD_STATE.NEW;
            cmd.AddTime = DateTime.Now;
            this.sql.Command.AddN(cmd);
        }

    }
}

