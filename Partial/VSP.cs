using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Floware.Concurrent;
using Floware.HSMS;
using Floware.SECSFlow.Core.Object;
using Floware.Utils;
using TcsV2.Core;
using TcsV2.Core.MODEL;
using Floware.LINQ;


namespace TCS.ServerV2
{

    partial class FormMain
    {

        void InitVsp()
        {
            for ( ;;)
            {
                try
                {
                    if ( !SERVER_ACTIVE ) continue;

                    //Host State 확인

                    //PLC State 확인

                    //if (sql.Command.Count() == 0) continue;

                    var allCmdId = sql.Command.All.ToDictionary( x => x.ID );
                    var subAll = sql.SubCmd.All;

                    subAll.FwEach( x =>
                     {
                         if ( !allCmdId.ContainsKey( x.CmdID ) )
                         {
                             sql.SubCmd.DelbyCmdID( x.CmdID );
                             logger.W( "[VSP] Subcommand garbage will be deleted, {0}", x.CmdID );
                         }
                     } );

                    var routeAll = sql.Route.All.ToDictionary( x => x.Id );

                    allCmdId.Values.Where( x => !x.Assigned )
                        .FwEach( x =>
                         {
                             var allUnit = sql.Unit.All.ToDictionary( q => q.ID );

                            //if (allUnit[x.SrcUnitID].Status == E_UNIT_STATUS.EMPTY)
                            //{
                            //    //source unit Type = virtual
                            //    if (allUnit[x.SrcUnitID].IsVirtual)
                            //    {
                            //        sql.Unit.ChgStatus(x.SrcUnitID, E_UNIT_STATUS.FULL);
                            //    }
                            //}

                            if ( allUnit[x.DestUnitID].IsPortReal && !allUnit[x.DestUnitID].CanLoad )
                             {
                                 logger.W( "[VSP] DestPort[{0}] Status is Full or State Check...", x.DestUnitID );
                                 return;
                             }

                             var carrierId = sql.Carrier[x.CarrierID];

                             if ( carrierId == null )
                             {
                                 logger.W( "[VSP] Invalid Carrier = CID : {0}, LOC : {1}", x.CarrierID, x.SrcUnitID );
                                 return;
                             }
                             var v = string.Format( "{0}_{1}", x.SrcUnitID, x.DestUnitID );
                             TcsRoute route = routeAll.ContainsKey( v ) ? routeAll[v] : null;

                             if ( null == route )
                             {
                                 logger.W( "[VSP] route not exist:{0}", v );
                                 return;
                             }

                            //현재 carrier loc와 command srcUnit 비교하여 subcmd count 결정(생성 여부)
                            var fteSub = new List<TcsUnit>();
                             var mctSub = new List<TcsUnit>();

                             if ( !string.IsNullOrEmpty( route.FTE ) )
                             {
                                 fteSub = route.FTE.Split( ',' ).Select( y => allUnit[y] ).ToList();
                             }
                             if ( !string.IsNullOrEmpty( route.MCT ) )
                             {
                                 mctSub = route.MCT.Split( ',' ).Select( y => allUnit[y] ).ToList();
                             }
                             int ifteAble = 1;
                             int imctAble = 1;

                            //var allCmds = sql.Command.All.ToDictionary(z => z.ID);
                            if ( !string.IsNullOrEmpty( route.FTE ) )
                             {
                                 var tmpFte = allCmdId
                                             .Where( z => !z.Key.Equals( x.ID ) && z.Value.MachineID.Equals( x.MachineID ) && z.Value.Assigned && !string.IsNullOrEmpty( z.Value.VehicleID ) )
                                             .ToList();

                                 if ( tmpFte.Count == 0 )
                                 {
                                     ifteAble = 0;
                                 }
                                 else
                                 {
                                     tmpFte.FwEach( s =>
                                     {
                                        //if (sql.SubCmd[s.Value.VehicleID] == null) return;
                                        if ( subAll.Select( w => w.VehicleID ).Contains( s.Value.VehicleID ) )
                                         {
                                            //* 확인필요.
                                            ifteAble = tmpFte.Count( z => subAll.Where( w => w.VehicleID == z.Value.VehicleID ).First().Route.Split( ',' ).Select( y => allUnit[y] ).Last().ID.Equals( fteSub.Last().ID ) );
                                         }
                                     } );
                                 }
                             }

                             if ( !string.IsNullOrEmpty( route.MCT ) )
                             {
                                 var tmpMct = allCmdId
                                             .Where( z => !z.Key.Equals( x.ID ) && z.Value.Assigned && !string.IsNullOrEmpty( z.Value.VehicleID ) && z.Value.MachineID.Equals( x.MachineID ) )
                                             .ToList();

                                 if ( tmpMct.Count == 0 )
                                 {
                                     imctAble = 0;
                                 }
                                 else
                                 {
                                     tmpMct.FwEach( s =>
                                     {
                                         if ( subAll.Select( w => w.VehicleID ).Contains( s.Value.VehicleID ) )
                                         {
                                             imctAble = tmpMct.Count( z => subAll.Where( q => q.VehicleID == z.Value.VehicleID ).First().Route.Split( ',' ).Select( y => sql.Unit[y] ).Last().ID.Equals( mctSub != null ? mctSub.Last().ID : string.Empty ) );
                                         }
                                     } );
                                 }
                             }

                             if ( fteSub.Count > 0 ? carrierId.CurrUnitID == fteSub.First().ID : false )
                             {
                                 if ( ifteAble == 0 )
                                     x.TspUnitFTE = fteSub;
                             }
                             else
                             {
                                 if ( imctAble == 0 )
                                     x.TspUnitMCT = mctSub;
                             }

                             if ( x.TspUnitFTE.Count > 1 )
                             {
                                 x.TspUnitFTE.FwEach( s =>
                                 {
                                     if ( x.SrcUnitID.Equals( s.ID ) ) return;
                                     if ( carrierId.CurrUnitID != s.ID )
                                         if ( s.UnitType == E_UNIT_TYPE.PORT_MCT && !s.CanLoad )
                                         {
                                             logger.W( "[VSP] {0} Port Not available Load", s.ID );
                                             return;
                                         }
                                 } );
                             }

                             if ( x.TspUnitMCT.Count > 1 )
                             {
                                 x.TspUnitMCT.FwEach( s =>
                                 {
                                     if ( x.SrcUnitID.Equals( s.ID ) ) return;

                                     if ( carrierId.CurrUnitID != s.ID )
                                         if ( s.UnitType == E_UNIT_TYPE.PORT_MCT && !s.CanLoad )
                                         {
                                             logger.W( "[VSP] {0} Port Not available Load", s.ID );
                                             return;
                                         }
                                 } );
                             }

                             if ( x.TspUnitFTE.Count > 1 && (subAll.Count == 0 ? true : !subAll.Select( q => q.VehicleID ).Contains( x.Machine.FTE.ID )) )
                                 sql.SubCmd.AddN( new TcsSubCmd { CmdID = x.ID, Route = route.FTE, VehicleID = x.Machine.FTE.ID, } );
                             if ( x.TspUnitMCT.Count > 1 && (subAll.Count == 0 ? true : !subAll.Select( q => q.VehicleID ).Contains( x.Machine.MCT.ID )) )
                                 sql.SubCmd.AddN( new TcsSubCmd { CmdID = x.ID, Route = route.MCT, VehicleID = x.Machine.MCT.ID, } );
                         } );
                }
                catch ( Exception e )
                {
                    logger.E( string.Format( "[VSP] {0} - {1}", e.StackTrace, e.Source ) );
                }
                finally
                {
                    LockUtils.Wait( 1000 );
                }
            }
        }
    }
}
