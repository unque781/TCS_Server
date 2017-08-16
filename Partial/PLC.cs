using System;
using System.Collections.Generic;
using System.Linq;
using Floware.Concurrent;
using Floware.LINQ;
using Floware.Logging;
using Floware.PLC;
using Floware.PLC.Model;
using Floware.PLC.SLMP;
using Floware.PLC.XLS;
using Floware.SQL;
using Floware.Utils;
using TcsV2.Core;
using TcsV2.Core.DTO;
using TcsV2.Core.MODEL;
using System.Threading;
using System.Diagnostics;

namespace TCS.ServerV2
{

    partial class FormMain
    {

        Dictionary<string, TcsAlarm> ddMemAlarm = new Dictionary<string, TcsAlarm>();
        MultiFpc plc = new MultiFpc();
        StandAloneNss xls = new StandAloneNss();
        private const string SPLIT_PREFIX = "#";

        class XlsB : XlsBitBlock
        {
            public E_PLC_KIND Kind { get; set; }
        }

        class XlsW : XlsWordBlock
        {
            public E_PLC_KIND Kind { get; set; }
        }

        void AddLogger(TcsMachine m)
        {
            LogUtils.AddAppender(m.ID, new FileAppender { File = string.Format(@"D:\TCS\LOG\SVR\PLC\{0}.log", m.ID), });
        }

        void InitPLC()
        {
            try
            {
                
                sql.Machine.All
                .FwEach(x => AddLogger(x))
                ;

                sql.Machine.All
                    .FwEach(x => sql.Machine.ChgComm(x.ID, E_CONN_STATE.DIS_CONNECT))
                    //.Where(x => !x.NotUse)
                    .FwEach(x => InitMap(x));
                
                plc.OnBitChanged += _OnBitChanged;
                plc.OnWordChanged += _OnWordChanged;
                plc.OnConnect += _OnConnect;
                plc.OnDisconnect += _OnDisconnect;
                plc.OnWriteComplete += (_OnWrited);

                plc.Connect();
            }
            catch (Exception e)
            {
                logger.E("[TCS] " + e.Message);
            }
        }

        void InitMap(TcsMachine m)
        {
            var miz = new SlmpManager();
            miz.Config.Id = m.ID;
            miz.Config.IpAddress = m.IPAddr;
            miz.Config.Port = m.PortNo;
            miz.Config.RollingCount = 3;
            var llB = xls.Nss.ListT<XlsB>("SELECT * FROM [BB$]")
                   .Where(x => !string.IsNullOrEmpty(x.TagName))
                   .ToList()
                   ;
            var llW = xls.Nss.ListT<XlsW>("SELECT * FROM [WW$]")
                .Where(x => !string.IsNullOrEmpty(x.TagName))
                .ToList()
                ;

            var grpB = new SlmpGroup { Name = "BB", Device = SlmpDevice.B, };
            var grpW = new SlmpGroup { Name = "WW", Device = SlmpDevice.W, };

            MapBit(m, grpB, llB);
            MapWord(m, grpW, llW);

            miz.AddGroup(grpB);
            miz.AddGroup(grpW);
            miz.DelayAutoOff = 1000;
            miz.OnConnect += Miz_OnConnect;
            miz.OnFirstColtd += Miz_OnFirstColtd;            
            miz.HeartBeat(m.ID + "#CIMALIVE", 1000);
            
            plc.Add(miz);
        }

        private void Miz_OnConnect(string id)
        {
            //logger.I(string.Format("[TCS] PLC Connect...{0}", id));

            //var units = sql.Unit.All.Where(x => x.MachineID.Equals(id) && x.IsPortReal);
            //var dbCarrier = sql.Carrier.All.Where(x => x.CurrUnit.MachineID.Equals(id)).ToDictionary(x => x.CurrUnitID);
            //var dbCommands = sql.Command.All.Where(x => x.MachineID.Equals(id)).ToDictionary(x => x.CarrierID);

            //Dictionary<string, string> plcCarrier = new Dictionary<string, string>();

            //units.FwEach(x =>
            //{
            //    if (!string.IsNullOrEmpty(plc.ReadWord(x.ID + SPLIT_PREFIX + "CARRIERID").Value.Trim()))
            //        plcCarrier.Add(plc.ReadWord(x.ID + SPLIT_PREFIX + "CARRIERID").Value.Trim(), x.ID);
            //});

            //dbCarrier.Keys.FwEach(x =>
            //{
            //    TcsCarrier c = dbCarrier[x];

            //    if (!plcCarrier.ContainsKey(c.ID))
            //    {
            //        //Command 정리
            //        if (dbCommands.ContainsKey(c.ID))
            //        {
            //            logger.I(string.Format("[TCS] TCS Not Exsits Carrier [{0}] so,, Command [{1}] Delete !!", c.ID, dbCommands[c.ID].ID));
            //            TcsCancel(dbCommands[c.ID], true, "TCS");
            //        }
            //        //remove
            //        logger.I(string.Format("[TCS] PLC Not Exsits Carrier[{0}] so,, Carrier [{0}] Removed!!", c.ID));
            //        this.RemoveCarrier(c.ID, x, E_IDREAD_STATUS.NO_CST);
            //    }
            //    else
            //    {
            //        if (c.CurrUnitID.Equals(plcCarrier[c.ID]))
            //        {
            //            if (dbCommands.ContainsKey(c.ID))
            //            {
            //                if (units.FirstOrDefault(t => t.ID.Equals(x)).UnitType != E_UNIT_TYPE.PORT_MCT)
            //                {
            //                    logger.I(string.Format("[TCS] TCS Not Exsits Carrier [{0}] so,, Command [{1}] Delete !!", c.ID, dbCommands[c.ID].ID));
            //                    TcsCancel(dbCommands[c.ID], true, "TCS");
            //                }
            //                return;
            //            }
            //        }
            //        else
            //        {
            //            if (sql.Command.HasByCarrierID(c.ID))
            //            {
            //                if (plcCarrier[c.ID] == sql.Command.GetByCarrierID(c.ID).DestUnitID)
            //                {
            //                    logger.I(string.Format("[TCS] Current Loc is DestUnit Delete Command {0}", dbCommands[c.ID]));
            //                    TcsCancel(dbCommands[c.ID], true, "TCS");
            //                }
            //            }
            //            logger.I(string.Format("[TCS] Carrier Location Change [{0}] -> [{1}] ]", c.CurrUnitID, plcCarrier[c.ID]));
            //            sql.Carrier.ChgLocation(c.ID, plcCarrier[c.ID]);
            //        }
            //    }
            //});

            //plcCarrier.Keys.FwEach(x =>
            //{
            //    if (dbCarrier.Values.Where(s => s.ID.Equals(x)).Count() == 0)
            //    {
            //        //install

            //        //case1 normal
            //        logger.I(string.Format("[TCS] TCS Not Exsits Carrier so,, Carrier [{0}], Loc [{1}] Install!!", x, plcCarrier[x]));
            //        this.InstallCarrier(x, plcCarrier[x], E_IDREAD_STATUS.SUCCESS);

            //        //case2 doublestorage
            //    }
            //});
        }

        private void Miz_OnFirstColtd(PFConfig arg)
        {

            logger.I(string.Format("[TCS] First Scan...{0}", arg.Id));

            var units = sql.Unit.All.Where(x => x.MachineID.Equals(arg.Id) && x.IsPortReal);

            if (sql.Carrier.All.Count() > 0)
            {
                var dbCarrier = sql.Carrier.All.Where(x => x.CurrUnit.MachineID.Equals(arg.Id)).ToDictionary(x => x.ID);

                Dictionary<string, string> plcCarrier = new Dictionary<string, string>();

                units.FwEach(x =>
                {
                    var wUnitStatus = plc.ReadWord(x.ID + SPLIT_PREFIX + "STATUS");         //170511 Kkm PLC에서 상태를 읽어온다.

                    if (EnumUtils.Chg<E_UNIT_STATUS>(wUnitStatus.IntValue - 1) == E_UNIT_STATUS.FULL)       //170511 Kkm Unit Status가 Full일 경우
                    {
                         string carrierID = string.Empty;
                         if ( x.UnitType == E_UNIT_TYPE.FTE || x.UnitType == E_UNIT_TYPE.MCT )
                             carrierID = plc.ReadWord( x.ID + SPLIT_PREFIX + "PLCCARRIERID" ).Value.Trim();
                         else
                             carrierID = plc.ReadWord( x.ID + SPLIT_PREFIX + "CARRIERID" ).Value.Trim();

                         if ( !string.IsNullOrEmpty( carrierID ) )
                         {
                             plcCarrier.Add( carrierID, x.ID );
                             logger.I( string.Format( "[PLC] Carrier List Add - UnitID [{0}], CarrierID [{1}]", x.ID, carrierID ) );
                         }
                     }
                 } );


                dbCarrier.Keys.FwEach(x =>
                {
                    TcsCarrier c = dbCarrier[x];

                    if (!plcCarrier.ContainsKey(c.ID))      //DB Carrier가 실제 PLC Carrier에 없는 경우
                    {
                        //Command 정리
                        if (sql.Command.All.Count > 0)
                        {
                            var dbCommands = sql.Command.All.Where(y => y.MachineID.Equals(arg.Id)).ToDictionary(y => y.CarrierID);
                            if (dbCommands.ContainsKey(c.ID))
                            {
                                logger.I(string.Format("[TCS] TCS Not Exsits Carrier [{0}] so,, Command [{1}] Delete !!", c.ID, dbCommands[c.ID].ID));
                                TcsCancel(dbCommands[c.ID], true, "TCS");
                            }
                        }
                        //remove
                        logger.I(string.Format("[TCS] PLC Not Exsits Carrier[{0}], Unit = [{1}] so,, Carrier [{0}] Removed!!", c.ID, c.CurrUnitID));
                        this.RemoveCarrier(c.ID, x, E_IDREAD_STATUS.NO_CST);
                    }
                    else
                    {
                        if (plcCarrier.ContainsValue(c.CurrUnitID))     //DB Carrier의 UnitID와 PLC Carrier의 UnitID 일치
                        {
                            if (sql.Command.All.Count > 0)
                            {
                                var dbCommands = sql.Command.All.Where(y => y.MachineID.Equals(arg.Id)).ToDictionary(y => y.CarrierID);
                                if (dbCommands.ContainsKey(c.ID))
                                {
                                    if (units.FirstOrDefault(t => t.ID.Equals(x)).UnitType != E_UNIT_TYPE.PORT_MCT)
                                    {
                                        logger.I(string.Format("[TCS] TCS Not Exsits Carrier [{0}] so,, Command [{1}] Delete !!", c.ID, dbCommands[c.ID].ID));
                                        TcsCancel(dbCommands[c.ID], true, "TCS");
                                    }
                                    return;
                                }
                            }
                        }
                        else
                        {
                            if (sql.Command.HasByCarrierID(c.ID))
                            {
                                if (sql.Command.All.Count > 0)
                                {
                                    var dbCommands = sql.Command.All.Where(y => y.MachineID.Equals(arg.Id)).ToDictionary(y => y.CarrierID);
                                    if (plcCarrier[c.ID] == sql.Command.GetByCarrierID(c.ID).DestUnitID)
                                    {
                                        logger.I(string.Format("[TCS] Current Loc is DestUnit Delete Command {0}", dbCommands[c.ID]));
                                        TcsCancel(dbCommands[c.ID], true, "TCS");
                                    }
                                }
                                logger.I(string.Format("[TCS] Carrier Location Change [{0}] -> [{1}] ]", c.CurrUnitID, plcCarrier[c.ID]));
                                sql.Carrier.ChgLocation(c.ID, plcCarrier[c.ID]);

                            }
                        }
                    }
                });

                plcCarrier.Keys.FwEach(x =>
                {
                    if (dbCarrier.Values.Where(s => s.ID.Equals(x)).Count() == 0)
                    {
                    //install

                    //case1 normal
                    logger.I(string.Format("[TCS] TCS Not Exsits Carrier so,, Carrier [{0}], Loc [{1}] Install!!", x, plcCarrier[x]));
                        this.InstallCarrier(x, plcCarrier[x], E_IDREAD_STATUS.SUCCESS);

                    //case2 doublestorage
                }
                });
            }
        }

        void MapBit(TcsMachine m, SlmpGroup grp, List<XlsB> llB)
        {

            //SubText을 기준으로
            var ddSubText = llB.Where(x => !string.IsNullOrEmpty(x.SubText)).FwGroupList(x => x.SubText);

            ddSubText["MACHINE"].FwEach(x =>
                {
                    x.TagName = m.ID + SPLIT_PREFIX + x.TagName;
                    var b = grp.AddBitBlock(x);
                    b.KindE = x.Kind;
                    b.SubObject = m;
                });

            var fte = sql.Unit.FTE(m.ID);
            var mct = sql.Unit.MCT(m.ID);

            ddSubText["FTE"].FwEach(x =>
                {
                    x.TagName = fte.ID + SPLIT_PREFIX + x.TagName;
                    var b = grp.AddBitBlock(x);
                    b.KindE = x.Kind;
                    b.SubObject = fte;
                });

            ddSubText["MCT"].FwEach(x =>
                {
                    x.TagName = mct.ID + SPLIT_PREFIX + x.TagName;
                    var b = grp.AddBitBlock(x);
                    b.KindE = x.Kind;
                    b.SubObject = mct;
                });

            {
                var u = sql.Unit[m.ID + "_B01"];
                ddSubText["B01"]
                        .FwEach(x =>
                        {
                            x.TagName = u.ID + SPLIT_PREFIX + x.TagName;
                            var b = grp.AddBitBlock(x);
                            b.KindE = x.Kind;
                            b.SubObject = u;
                        });
            }

            {
                var u = sql.Unit[m.ID + "_B02"];
                ddSubText["B02"]
                        .FwEach(x =>
                        {
                            x.TagName = u.ID + SPLIT_PREFIX + x.TagName;
                            var b = grp.AddBitBlock(x);
                            b.KindE = x.Kind;
                            b.SubObject = u;
                        });
            }

            {
                var u = sql.Unit[m.ID + "_B03"];
                ddSubText["B03"]
                        .FwEach(x =>
                        {
                            x.TagName = u.ID + SPLIT_PREFIX + x.TagName;
                            var b = grp.AddBitBlock(x);
                            b.KindE = x.Kind;
                            b.SubObject = u;
                        });
            }
        }

        void MapWord(TcsMachine m, SlmpGroup grp, List<XlsW> llW)
        {

            var ddSubText = llW.Where(x => !string.IsNullOrEmpty(x.SubText)).FwGroupList(x => x.SubText);

            ddSubText["MACHINE"].FwEach(x =>
            {
                x.TagName = m.ID + SPLIT_PREFIX + x.TagName;
                var b = grp.AddWordBlock(x);
                b.KindE = x.Kind;
                b.SubObject = m;
            });

            var fte = sql.Unit.FTE(m.ID);
            var mct = sql.Unit.MCT(m.ID);

            ddSubText["FTE"].FwEach(x =>
            {
                x.TagName = fte.ID + SPLIT_PREFIX + x.TagName;
                var b = grp.AddWordBlock(x);
                b.KindE = x.Kind;
                b.SubObject = fte;
            });

            ddSubText["MCT"].FwEach(x =>
            {
                x.TagName = mct.ID + SPLIT_PREFIX + x.TagName;
                var b = grp.AddWordBlock(x);
                b.KindE = x.Kind;
                b.SubObject = mct;
            });

            {
                var u = sql.Unit[m.ID + "_B01"];
                ddSubText["B01"]
                        .FwEach(x =>
                        {
                            x.TagName = u.ID + SPLIT_PREFIX + x.TagName;
                            var b = grp.AddWordBlock(x);
                            b.KindE = x.Kind;
                            b.SubObject = u;
                        });
            }

            {
                var u = sql.Unit[m.ID + "_B02"];
                ddSubText["B02"]
                        .FwEach(x =>
                        {
                            x.TagName = u.ID + SPLIT_PREFIX + x.TagName;
                            var b = grp.AddWordBlock(x);
                            b.KindE = x.Kind;
                            b.SubObject = u;
                        });
            }

            {
                var u = sql.Unit[m.ID + "_B03"];
                ddSubText["B03"]
                        .FwEach(x =>
                        {
                            x.TagName = u.ID + SPLIT_PREFIX + x.TagName;
                            var b = grp.AddWordBlock(x);
                            b.KindE = x.Kind;
                            b.SubObject = u;
                        });
            }
        }

        void _OnBitChanged(BitBlock block)
        {
            Logger.GetLogger(block.ConfigID).I("[PLC->CIM] BitChd {0} {1}", block.ConfigID, block);

            if (!SERVER_ACTIVE) 
                return;

            ThreadUtils.InvokePoolEx(OnBitChanged, block);
        }

        void _OnWordChanged(WordBlock block)
        {
            Logger.GetLogger(block.ConfigID).I("[PLC->CIM] WordChd {0} {1}", block.ConfigID, block);

            if (!SERVER_ACTIVE) return;

            ThreadUtils.InvokePoolEx(OnWordChanged, block);
        }

        void _OnWrited(Block block)
        {
            Logger.GetLogger(block.ConfigID).I("[CIM->PLC] Writed {0} {1}", block.ConfigID, block);
        }

        void _OnConnect(string id)
        {
            if (!SERVER_ACTIVE) return;

            var llv = sql.Unit.GetVehicle(id);
            foreach (var v in llv)
            {
                this.plc.WriteWord(v.ID + SPLIT_PREFIX + E_PLC_TAG.VEHICLEID, v.StationID);
                SendVehicleLocationToWeb(v.ID, v.StationID);
            }
            this.sql.Machine.ChgComm(id, E_CONN_STATE.CONNECT);
            SendToWeb(new PlcCommDto { Machine = this.sql.Machine[id] }.PushMsg);    
            sql.Command.All.Where(x =>x.MachineID == id).FwEach(x=>
            {   
                if(x.Assigned)
                {
                    sql.Command.ChgAssigned(x.ID, false);
                }
            });

        }

        void _OnDisconnect(string id)
        {
            if (!SERVER_ACTIVE) return;

            if (!sql.Machine.Has(id)) return;
            this.sql.Machine.ChgComm(id, E_CONN_STATE.DIS_CONNECT);
            //this.sql.Machine.ChgState(id, E_MACHINE_STATE.UNK);

            //sql.Unit.GetVehicle(id).FwEach(x =>
            //{
            //    if (this.VrpTh.ContainsKey(x.ID))
            //    {
            //        var th = this.VrpTh[x.ID];
            //        try
            //        {
            //            th.Abort();
            //        }
            //        catch (ThreadAbortException e)
            //        {
            //            logger.I(e.Message);
            //        }
            //        this.VrpTh.Remove(x.ID);
            //        logger.I("VRP Stop... VehicleID : {0} ", x.ID);
            //    }
            //});
            SendToWeb(new PlcCommDto { Machine = this.sql.Machine[id] }.PushMsg);
        }

        void OnBitChanged(BitBlock block)
        {
            try
            {
                E_PLC_KIND kind = (E_PLC_KIND)block.KindE;

                string[] msg = block.Name.Split('#');
                
                if (!block.IsBitOn) 
                    return;

                //Machine관련.
                if (kind == E_PLC_KIND.B_MACHINE_EVENT)
                {
                    this.HandleMachine(msg, block);
                    return;
                }

                //Unit관련.
                if (kind == E_PLC_KIND.B_UNIT_EVENT)
                {
                    TcsUnit u = sql.Unit[msg[0]];
                    string carrierID = plc.ReadWord(u.ID + SPLIT_PREFIX + "CARRIERID").Value.Trim();
                    if (sql.Command.HasByCarrierID(carrierID)) return;
                    //2017 02 17 by mc
                    //description - 현재 unit 의 carrierID -> DB DATA 와 PLCCARRIERID 실제 READ 된 DATA 가 다를 경우 RETURN
                    //사유        - 이경우 MISMATCH 인경우 임. VRP 에서 처리 되어야하나 위 조건문에서 걸리지 않고 IDREADACK 메소드 호출하게 되어 조건 추가.
                    //TcsCarrier c = sql.Carrier.GetCarrierByUnit(u.ID);
                    //if(c!=null)
                    //{
                    //    if (c.ID != carrierID) return;
                    //}
                    

                    switch (msg[1])
                    {
                        case "IDREADACK":
                            this.IDReadAck(u, carrierID);

                            break;
                        case "IDREADFAIL":
                            this.IDReadFail(u, carrierID);
                            break;
                    }
                    return;
                }

                //Vehicle 관련.
                if (kind == E_PLC_KIND.B_VEHICLE_EVENT)
                {
                    this.HandleVehicle(msg, block);
                }
            }
            catch (Exception e)
            {
                logger.E("[TCS] " + e.Message);
            }
        }

        void HandleVehicle(string[] msg, BitBlock block)
        {
            try
            {
                if (Enum.IsDefined(typeof(E_VRP_HANDLE_MSG), msg[1]))
                    return;
                TcsUnit v = sql.Unit[msg[0]];
                TcsMachine m = sql.Machine[v.MachineID];
                string plcCarrier = plc.ReadWord(v.ID + SPLIT_PREFIX + "PLCCARRIERID").Value.Trim();
                TcsCarrier carrier =  string.IsNullOrEmpty(plcCarrier) ? sql.Carrier.GetCarrierByUnit(v.ID) : sql.Carrier.GetCarrierByPlcCarrier(plcCarrier);

                if (carrier == null)
                {
                    logger.I("[TCS] CarrierID is Null");
                    return;
                }
                Assert.IsTrue(sql.Command.Has(carrier.CommandID), "CarrierID : {0} Command Not Exist!!", carrier.ID);
                TcsCommand cmd = this.sql.Command[carrier.CommandID];

                switch (msg[1])
                {
                    case "SRC_ARRIVED":
                        this.sql.Command.ChgSocArrivedTime(cmd.ID);
                        this.sql.Command.ChgState(cmd.ID, E_CMD_STATE.SRC_ARRIVED);
                        sql.Unit.ChgSubState(v.ID, E_VEHICLE_SUB_STATE.PARKED);
                        this.EventRpt6(H_CEID.VEHICLE_ARRIVED_601, cmd, cmd.SrcUnitID);

                        break;
                    case "DEPARTED":
                        this.sql.Command.ChgState(cmd.ID, E_CMD_STATE.DEPARTED);
                        this.sql.Unit.ChgSubState(v.ID, E_VEHICLE_SUB_STATE.ENROUTE);
                        this.EventRpt6(H_CEID.VEHICLE_DEPART_605, cmd, cmd.DestUnitID);

                        this.SendVehicleSubStateToWeb(v.ID);

                        break;
                    case "DEST_ARRIVED":
                        this.sql.Command.ChgDstArrivedTime(cmd.ID);
                        this.sql.Command.ChgState(cmd.ID, E_CMD_STATE.DEST_ARRIVED);
                        this.sql.Unit.ChgSubState(v.ID, E_VEHICLE_SUB_STATE.PARKED);
                        this.EventRpt6(H_CEID.VEHICLE_ARRIVED_601, cmd, cmd.DestUnitID);

                        this.SendVehicleSubStateToWeb(v.ID, cmd.DestUnitID);

                        break;
                    default:
                        break;
                }

                this.SendCommandToWeb();
            }
            catch (Exception e)
            {
                logger.E("[TCS] {0}",e.Message);
                logger.E("[TCS] handle vehicle msg = {0}", block);
            }
        }
        

        private void VirtualPortHandle(TcsCommand cmd, TcsUnit unit)
        {
            TcsCommand nCmd = sql.Command[cmd.ID];

            switch (nCmd.State)
            {
                case E_CMD_STATE.INSTALLED:
                    sql.Carrier.ChgLocation(nCmd.CarrierID, nCmd.VehicleID);
                    sql.Unit.ChgStatus(unit.ID, E_UNIT_STATUS.EMPTY);
                    break;
                case E_CMD_STATE.REMOVED:
                    sql.Carrier.ChgLocation(nCmd.CarrierID, unit.ID);
                    sql.Unit.ChgStatus(unit.ID, E_UNIT_STATUS.FULL);
                    break;
                default:
                    break;
            }

            this.SendUnitStatusToWeb(unit.ID);
        }

        void HandleMachine(string[] msg, BitBlock block)
        {
            TcsMachine m = sql.Machine[block.ConfigID];
            string plcCarrier = plc.ReadWord(m.ID + SPLIT_PREFIX + E_PLC_TAG.PLCCARRIERID).Value.Trim();
            string carrierID = sql.Carrier.GetCarrierByPlcCarrier(plcCarrier).ID;

            bool hasCarrier = sql.Carrier.Has(carrierID);
            TcsCarrier carrier = hasCarrier ? this.sql.Carrier[carrierID] : null;

            TcsCommand command = hasCarrier ? this.sql.Command[carrier.CommandID] : this.sql.Command.GetByCarrierID(carrierID);

            switch (msg[1])
            {

                default:
                    break;
            }
            this.SendCommandToWeb();
        }
        
        void OnWordChanged(WordBlock block)
        {
            try
            {
                E_PLC_KIND kind = (E_PLC_KIND)block.KindE;
                string[] msg = block.Name.Split('#');


                switch (kind)
                {
                    case E_PLC_KIND.M_STATE_CHGD:
                        var m = block.SubObject as TcsMachine;//sql.Machine[msg[0]];
                        this.MachineStateChange(m, block);
                        break;
                    case E_PLC_KIND.V_STATE_CHGD:
                        {
                            var v = block.SubObject as TcsUnit; //sql.Vehicle[msg[0]];
                            this.VehicleStateChange(v, block);
                        }
                        break;
                    case E_PLC_KIND.V_STATUS_CHGD:
                        {
                            var v = block.SubObject as TcsUnit; //sql.Vehicle[msg[0]];
                            this.VehicleStatusChange(v, block);
                        }
                        break;
                    case E_PLC_KIND.V_POINT_CHGD:
                        {
                            var v = block.SubObject as TcsUnit; //sql.Vehicle[msg[0]];
                            this.VehicleLocationChange(v, block.Value);
                        }
                        break;
                    case E_PLC_KIND.V_TRAVEL_CHGD:
                        {
                            var v = block.SubObject as TcsUnit; //sql.Vehicle[msg[0]];
                            this.VehicleTotalTravelChange(v, block.Value);
                        }
                        break;
                    case E_PLC_KIND.U_STATE_CHGD:
                        {
                            var u = block.SubObject as TcsUnit; //sql.Unit[msg[0]];
                            this.UnitStateChange(u, block);
                        }
                        break;
                    case E_PLC_KIND.U_STATUS_CHGD:
                        {
                            var u = block.SubObject as TcsUnit; //sql.Unit[msg[0]];
                            this.UnitStatusChange(u, block);
                        }
                        break;
                    //case E_PLC_KIND.U_LOG:
                    //    {
                    //        var u = block.SubObject as TcsUnit;
                    //        ValidationCarrier(u,block);
                    //    }
                    //    break;
                    //case E_PLC_KIND.V_LOG:
                    //    {
                    //        var u = block.SubObject as TcsUnit;
                            
                    //        if (msg[1] == E_PLC_TAG.PLCCARRIERID.ToString())
                    //            ValidationCarrier(u,block);
                    //    }
                    //    break;           
                    default:
                        break;
                }
            }
            catch (Exception e)
            {
                logger.E("[TCS] " + e.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="command"></param>
        /// <param name="resultCode"></param>
        /// <param name="isSourcePio"></param>
        /// <param name="isDestPio"></param>
        void TransferComplete(TcsCommand command, int resultCode, bool isSourcePio, bool isDestPio)
        {
            command = this.sql.Command[command.ID];
            command.ResultCode = resultCode;
            if (resultCode == 0) this.sql.Command.ChgHostState(command.ID, H_HOST_STATE.COMPLETE);

            if (isSourcePio) this.sql.Command.ChgHostState(command.ID, H_HOST_STATE.SOC_PIO_TIMEOUT);

            if (isDestPio) this.sql.Command.ChgHostState(command.ID, H_HOST_STATE.DEST_PIO_TIMEOUT);

            //command 정보를 가져온다.
            //먼저 Message을 전송해야함.
            this.EventRpt3(H_CEID.TRANSFER_COMP_207, command);
            sql.Carrier.ChgCommandID(command.CarrierID, "");
            this.sql.Command.ChgState(command.ID, E_CMD_STATE.TRANSFER_COMP);
            this.sql.HisCommand.AddHis(this.sql.Command[command.ID]);
            this.sql.Command.Delete(command.ID);

            this.SendCommandToWeb();

            var sourceUnit = sql.Unit[command.SrcUnitID];
            var destUnit = sql.Unit[command.DestUnitID];

            this.DeleteVirtualPortData( command.Carrier );

            //bool isUnit = sql.Unit.Has( command.DestUnitID );
            //if ( !isUnit ) return;         

            //TcsUnit destUnit = sql.Unit[command.DestUnitID];
            //if ( !destUnit.IsPortReal )
            //{
            //    if ( resultCode == 0 )
            //    {
            //        sql.Carrier.Del( command.CarrierID );
            //        this.sql.Unit.ChgStatus( destUnit.ID, E_UNIT_STATUS.EMPTY );
            //        this.SendUnitStatusToWeb( destUnit.ID );
            //    }
            //}
            //else
            //{
            //    this.sql.Carrier.ChgCommandID( command.CarrierID, "" );
            //}
        }

        private void TransferAbortByEQ(TcsCommand command)
        {
            //EQ에서 반송을 못할때.
            this.sql.Command.ChgByWho(command.ID, E_BYWHO.EQP);
            this.sql.Command.ChgHostState(command.ID, H_HOST_STATE.AUTO_CANCEL);

            //변경된 Command을 다시 가져온다.
            command = this.sql.Command[command.ID];
            this.EventRpt3(H_CEID.TRANSFER_ABORT_COMP_201,command);

            sql.Command.ChgState(command.ID, E_CMD_STATE.ABORT);
            sql.HisCommand.AddHis(sql.Command[command.ID]);
            sql.Command.Delete(command.ID);
        }

        //Logic Vehicle 관련.
        private void VehicleTotalTravelChange(TcsUnit v, string value)
        {
            sql.Unit.ChgVehicleTotalTravel(v.ID, value);
        }

        private void VehicleLocationChange(TcsUnit v, string value)
        {
            if (value.Length == 1)
                return;

            bool hasPoint = sql.Unit.HasStationID(value);
            if (!hasPoint)
                return;

            int stationID = int.Parse(value.Trim());

            //sql.Unit.ChgLocation(v.ID, value);

            this.SendVehicleLocationToWeb(v.ID, stationID);
        }


        private void VehicleStatusChange(TcsUnit v, WordBlock block)
        {
            //enum 0:empty, 1: full, plc 2:full,1:empty
            var e = EnumUtils.Chg<E_UNIT_STATUS>(block.IntValue - 1);
            sql.Unit.ChgStatus(v.ID, e);
            if (e == E_UNIT_STATUS.FULL)
            {
                if (sql.Command.ListN(x => x.VehicleID.Equals(v.ID)).Count == 0)
                {
                    string carrierID = plc.ReadWord(v.ID + SPLIT_PREFIX + "PLCCARRIERID").Value.Trim();

                    if (sql.Carrier.Has(carrierID))
                    {
                        //TcsCarrier carrier = sql.Carrier[carrierID];
                        this.sql.Carrier.ChgLocation(carrierID, v.ID);
                        this.EventRpt4(H_CEID.CARRIERINSTALL_301, sql.Carrier[carrierID], E_IDREAD_STATUS.SUCCESS);
                    }
                    else 
                    {
                        if (string.IsNullOrEmpty(carrierID))
                        {
                            carrierID = string.Format("UNKNOWN-{0}", DateTime.Now.ToString("yyyyMMddHHmmssff"));
                        }
                        this.InstallCarrier(carrierID, v.ID, E_IDREAD_STATUS.SUCCESS);
                    }
                    logger.W("[PLC] RESULT CARRIER-INSTALL COMPLETE CommandID : {0}", carrierID);
                }
                
            }
            else if (e == E_UNIT_STATUS.EMPTY)
            {
                var c = sql.Carrier.All.Where(x => string.IsNullOrEmpty(x.CommandID) && x.CurrUnitID.Equals(v.ID));

                c.FwEach(x =>
                {
                    logger.W("[TCS] Remove Garbage Carrier CarrierID : {0}, UnitID : {1}", x.ID, x.CurrUnitID);
                    this.RemoveCarrier(x.ID, x.CurrUnitID, E_IDREAD_STATUS.NO_CST);
                });
            }
            this.SendVehicleStatusToWeb(v.ID);
        }

        private void VehicleStateChange(TcsUnit v, WordBlock block)
        {
            //1 == idle , 2== down
            E_STATE state = E_STATE.NORMAL;
            switch (block.Value)
            {
                case "1":
                    state = E_STATE.NORMAL;
                    sql.Unit.ChgState(v.ID, state);
                    EventRpt8(H_CEID.VEHICLE_STATUS_CHG_611, v, E_VEHICLE_STATUS.DOWN);
                    break;
                case "2":
                    state = E_STATE.RUN;
                    sql.Unit.ChgState(v.ID, state);
                    EventRpt8(H_CEID.VEHICLE_STATUS_CHG_611, v, E_VEHICLE_STATUS.IDLE);
                    break;
                case "0":
                    state = E_STATE.NONE;
                    sql.Unit.ChgState(v.ID, state);
                    EventRpt8(H_CEID.VEHICLE_STATUS_CHG_611, v, E_VEHICLE_STATUS.DOWN);
                    break;
                case "3":
                    state = E_STATE.FAULT;
                    sql.Unit.ChgState(v.ID, state);
                    EventRpt8(H_CEID.VEHICLE_STATUS_CHG_611, v, E_VEHICLE_STATUS.DOWN);
                    break;
                case "4":
                    state = E_STATE.PM;
                    sql.Unit.ChgState(v.ID, state);
                    EventRpt8(H_CEID.VEHICLE_STATUS_CHG_611, v, E_VEHICLE_STATUS.DOWN);
                    break;
            }

            if (v.UnitType == E_UNIT_TYPE.FTE)
            {
                if (state != E_STATE.RUN)
                {
                    var o = sql.Unit.ListN(x => x.MachineID.Equals(v.MachineID) && v.UnitType == E_UNIT_TYPE.PORT_OHS).FirstOrDefault();
                    o.State = E_STATE.FAULT;
                    sql.Unit.ChgN(o);
                    this.EventRpt10(H_CEID.UNIT_OUTOFSERVICE_802, o, 2);
                    this.SendVehicleStateToWeb(o.ID);
                }
                else
                {
                    var o = sql.Unit.ListN(x => x.MachineID.Equals(v.MachineID) && v.UnitType == E_UNIT_TYPE.PORT_OHS).FirstOrDefault();
                    o.State = E_STATE.RUN;
                    sql.Unit.ChgN(o);
                    this.EventRpt10(H_CEID.UNIT_SERVICE_801, o, 2);
                    this.SendVehicleStateToWeb(o.ID);
                }
            }
            else if(v.UnitType == E_UNIT_TYPE.MCT)
            {
                if (state != E_STATE.RUN)
                {
                    var o = sql.Unit.ListN(x => x.MachineID.Equals(v.MachineID) && v.UnitType == E_UNIT_TYPE.PORT_EQP).FirstOrDefault();
                    o.State = E_STATE.FAULT;
                    sql.Unit.ChgN(o);
                    this.EventRpt10(H_CEID.UNIT_OUTOFSERVICE_802, o, 2);
                    this.SendVehicleStateToWeb(o.ID);
                }
                else
                {
                    var o = sql.Unit.ListN(x => x.MachineID.Equals(v.MachineID) && v.UnitType == E_UNIT_TYPE.PORT_EQP).FirstOrDefault();
                    o.State = E_STATE.RUN;
                    sql.Unit.ChgN(o);
                    this.EventRpt10(H_CEID.UNIT_SERVICE_801, o, 2);
                    this.SendVehicleStateToWeb(o.ID);
                }
            }

            if (state != E_STATE.NONE)
                this.SendVehicleStateToWeb(v.ID);
        }

        private void MachineStateChange(TcsMachine m, WordBlock block)
        {
            //1:Auto, 2:Manual
            var e = EnumUtils.Chg<E_MACHINE_STATE>(block.Value);
            sql.Machine.ChgState(m.ID, e);
            this.SendMachineStateToWeb(m.ID);
        }

        private void UnitStateChange(TcsUnit u, WordBlock block)
        {
            E_STATE state = E_STATE.NORMAL;
            if (u.UnitType == E_UNIT_TYPE.PORT_MCT)
            {
                //B03 OUT SERVICE 일경우 EQP PORT, MCT 같이 OUT SERVICE.
                //상위 보고는 B03만 하게 되어있음.
                var unit = sql.Unit.ListByMachineID(u.MachineID);
                switch (block.Value)
                {
                    case "1":
                        state = E_STATE.NORMAL;
                        sql.Unit.ChgState(u.ID, state);
                        EventRpt10(H_CEID.UNIT_OUTOFSERVICE_802, u, 1);
                        break;
                    case "2":
                        state = E_STATE.RUN;
                        sql.Unit.ChgState(u.ID, state);
                        EventRpt10(H_CEID.UNIT_SERVICE_801, u, 2);
                        break;
                    case "0":
                        state = E_STATE.NONE;
                        sql.Unit.ChgState(u.ID, state);
                        EventRpt10(H_CEID.UNIT_OUTOFSERVICE_802, u, 1);
                        break;
                    case "3":
                        state = E_STATE.FAULT;
                        sql.Unit.ChgState(u.ID, state);
                        EventRpt10(H_CEID.UNIT_OUTOFSERVICE_802, u, 1);
                        break;
                    case "4":
                        state = E_STATE.PM;
                        sql.Unit.ChgState(u.ID, state);
                        EventRpt10(H_CEID.UNIT_OUTOFSERVICE_802, u, 1);
                        break;
                }
                foreach(TcsUnit eqpUnit in unit)
                {
                    if (eqpUnit.UnitType == E_UNIT_TYPE.MCT)
                    {   //해당 MACHINE 의 EQP PORT, MCT 를 B03 STATE 와 같게 변경.
                        sql.Unit.ChgState(eqpUnit.ID, state);
                    }
                }
            }
            else
            {
                switch (block.Value)
                {
                    case "1":
                        state = E_STATE.NORMAL;
                        sql.Unit.ChgState(u.ID, state);
                        EventRpt10(H_CEID.UNIT_OUTOFSERVICE_802, u, 2);
                        break;
                    case "2":
                        state = E_STATE.RUN;
                        sql.Unit.ChgState(u.ID, state);
                        EventRpt10(H_CEID.UNIT_SERVICE_801, u, 2);
                        break;
                    case "0":
                        state = E_STATE.NONE;
                        sql.Unit.ChgState(u.ID, state);
                        EventRpt10(H_CEID.UNIT_OUTOFSERVICE_802, u, 1);
                        break;
                    case "3":
                        state = E_STATE.FAULT;
                        sql.Unit.ChgState(u.ID, state);
                        EventRpt10(H_CEID.UNIT_OUTOFSERVICE_802, u, 1);
                        break;
                    case "4":
                        state = E_STATE.PM;
                        sql.Unit.ChgState(u.ID, state);
                        EventRpt10(H_CEID.UNIT_OUTOFSERVICE_802, u, 1);
                        break;
                }
            }

            this.SendUnitStateToWeb(u.ID);
        }

        private void UnitStatusChange(TcsUnit u, WordBlock block)
        {

            //enum 0:empty, 1: full, plc 2:full,1:empty
            var e = EnumUtils.Chg<E_UNIT_STATUS>(block.IntValue - 1);
            Assert.IsFalse(EnumUtils.IntV(e) < 0, String.Format("UnitID : {0}, Invalid Status : {1}", u.ID, e));
            sql.Unit.ChgStatus(u.ID, e);
            this.SendUnitStatusToWeb(u.ID);

            //Manual로 자재를 들어내면, 정리해야함.
            if (e == E_UNIT_STATUS.FULL)
            {
                return;
            }

            //반송없이, Carrier 존재하면, Manual 로 처리하고, 있다고 생각해야함.
            bool isCarrier = this.sql.Carrier.HasByUnit(u.ID);
            if (!isCarrier) return;

            TcsCarrier carrier = sql.Carrier.GetCarrierByUnit(u.ID);
            
            //Command가 존재하고, NEW가 아니어야 함.
            bool isCmd = this.sql.Command.HasByCarrierID(carrier.ID);
            if (isCmd) return;

            logger.W("[TCS] Manual Carrier Remove Unit={0} CarrierID={1}", u.ID, carrier.ID);

            this.RemoveCarrier(carrier.ID, u.ID, E_IDREAD_STATUS.SUCCESS);
        }


        private void InstallCarrier(string cid, string unitID, E_IDREAD_STATUS status)
        {
            //carrier.AddN -> Has carrier 없을때만 하도록 변경.
            TcsUnit u = sql.Unit[unitID];

            if(!sql.Carrier.Has(cid))
                this.sql.Carrier.AddN(new TcsCarrier { ID = cid, CurrUnitID = unitID, });

            //if (u.Status == E_UNIT_STATUS.EMPTY && u.IsVirtual)
            //    this.sql.Unit.ChgStatus(u.ID, E_UNIT_STATUS.FULL);
            TcsCarrier nCarrier = sql.Carrier[cid];
            EventRpt4(H_CEID.CARRIERINSTALL_301, nCarrier, status);
        }

        private void RemoveCarrier(string carrierID, string unitID, E_IDREAD_STATUS status)
        {
            TcsUnit u = sql.Unit[unitID];
            TcsCarrier nCarrier = sql.Carrier[carrierID];
            sql.Carrier.Del(carrierID);
            //if (u.Status == E_UNIT_STATUS.FULL && u.IsVirtual)
            //    this.sql.Unit.ChgStatus(u.ID, E_UNIT_STATUS.EMPTY);
            this.EventRpt4(H_CEID.CARRIERREMOVED_302, nCarrier, status);
        }

        private void OnMoveReqAbort(string vID)
        {
            //2017.02.01 by mc
            //discription - cancel 시 plc 로 move_req_abort bit wirte 해주는 로직 추가.
            plc.WriteBit(vID + SPLIT_PREFIX + "MOVE_REQ_ABORT", true);
            LockUtils.Wait(1000);
            // PLCFROM, PLCTO 확인 이후 PLC 동작 모르기때문에 PLC 확인후 진행.
            /*
            string checkFrom = plc.ReadWord(v.ID + SPLIT_PREFIX + "PLCFROM").Value.Trim();
            string checkTo = plc.ReadWord(v.ID + SPLIT_PREFIX + "PLCTO").Value.Trim();
          
            if(string.IsNullOrEmpty(checkFrom) && string.IsNullOrEmpty(checkTo))
            {
                this.EventRpt3(H_CEID.TRANSFER_CANCEL_FAIL_205, c);
                return;
            }*/
            //end
        }
        void PlcAbort(TcsCommand cmd, bool abortBit, int resultCode)
        {
            TcsCommand c = this.sql.Command[cmd.ID];
            c.ResultCode = resultCode;
            TcsCarrier carrier = sql.Carrier[c.CarrierID];

            E_BYWHO byWho = E_BYWHO.EQP;
            this.sql.Command.ChgByWho(c.ID, byWho);

            H_HOST_STATE hostState = H_HOST_STATE.AUTO_ABORT;
            this.sql.Command.ChgHostState(c.ID, hostState);

            //State변경
            this.sql.Command.ChgState(c.ID, E_CMD_STATE.ABORT);

            //History Add
            this.sql.HisCommand.AddHis(this.sql.Command[c.ID]);

            this.EventRpt3(H_CEID.TRANSFER_ABORT_INIT_203, c);
            if (c.Assigned)
            {
                this.sql.Unit.ChgSubState(c.VehicleID, E_VEHICLE_SUB_STATE.NOTASSIGN);
                SendVehicleStatusToWeb(c.VehicleID);
                this.SendVehicleSubStateToWeb(c.VehicleID);
                this.EventRpt6(H_CEID.VEHICLE_UNASSIGN_610, c, c.SrcUnitID);
            }
            LockUtils.Wait(1000);
            EventRpt3(H_CEID.TRANSFER_ABORT_COMP_201, c);
            sql.Carrier.ChgCommandID(cmd.CarrierID, "");
            this.sql.Command.Delete(c.ID);
              
            var sourceUnit = sql.Unit[cmd.SrcUnitID];
            var destUnit = sql.Unit[cmd.DestUnitID];

            this.DeleteVirtualPortData( cmd.Carrier );
            //2017/04/02 by MC
            //cmd delete 시 carrier current Unit = virtual 이면 carrier 함께 삭제.
            //if ( carrier.CurrUnit.IsVirtual )
            //{
            //    sql.Carrier.Del( carrier.ID );
            //}

            if (abortBit)
            {
                OnMoveReqAbort(c.VehicleID);
            }
        }

        /// <summary>
        /// Virtual Port Data Delete
        /// </summary>
        /// <param name="unit"></param>
        /// <param name="carrierID"></param>
        /// <returns></returns>
        bool DeleteVirtualPortData( TcsCarrier carrier )
        {
            if ( carrier == null ) return false;
            var unit = carrier.CurrUnit;
            if ( unit == null ) return false;
            if ( !unit.IsVirtual ) return false;

            sql.Carrier.Del( carrier.ID );
            this.sql.Unit.ChgStatus( unit.ID, E_UNIT_STATUS.EMPTY );

            this.SendUnitStatusToWeb( unit.ID );

            logger.W( "[PLC] {0} Virtual Port [{1}] Carrier Data Deleted !", unit.ID, carrier.ID );

            return true;
        }
    }
}

