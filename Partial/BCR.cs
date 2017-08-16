using System;
using System.Collections.Generic;
using Floware.Concurrent;
using TcsV2.Core.MODEL;
using TcsV2.Core;
using System.Linq;
using Floware.LINQ;

namespace TCS.ServerV2
{

    partial class FormMain
    {
        void IDReadAck( TcsUnit u, string carrierID )
        {
            var subList = sql.SubCmd.All.FwGroupList( x => x.Route );
            bool isSkip = false;
            subList.Keys.FwEach( x =>
             {
                 logger.W( "Route:{0}", x );
                 if ( x.IndexOf( u.ID ) > 0 )
                 {
                     isSkip = true;
                     return;
                 }
             } );

            if ( isSkip )
            {
                logger.W( "[BCR] UnitID: {0}, Exist CommandID and Skip BCR", u.ID );
                return;
            }

            IList<TcsCarrier> carrierList = this.sql.Carrier.GetForLocation( u.ID );
            foreach ( var c in carrierList )
            {
                //같은 위치에 carrier 있으면 삭제.
                if ( c.ID.Equals( carrierID ) ) continue;
                logger.W( "[BCR] Remove Garbage Carrier : {0}", c.ID );
                this.RemoveCarrier( c.ID, u.ID, E_IDREAD_STATUS.MISSMATCH );
            }

            bool isCarrier = sql.Carrier.Has( carrierID );
            if ( isCarrier )
            {
                //Carrier Has Command => Dup, otherwise Carrier Loc change - kbs 6/14
                TcsCarrier ca = sql.Carrier[carrierID];

                if ( !string.IsNullOrEmpty( ca.CommandID ) )
                {
                    logger.W( "[BCR] Carrier Has CommadID : {0}", carrierID );
                    return;
                }

                if ( !ca.ID.Equals( carrierID ) )
                {
                    logger.W( "[BCR] Install Carrier : {0}", carrierID );
                    this.InstallCarrier( ca.ID, u.ID, E_IDREAD_STATUS.SUCCESS );
                }
                sql.Carrier.ChgLocation( ca.ID, u.ID );

                SendUnitStatusToWeb( u.ID );
                SendUnitStatusToWeb( ca.CurrUnitID );
            }
            else
            {
                //Carrier Install 처리하면 됨.
                if ( !u.IsVirtual ) // kbs 06/14
                {
                    logger.W( "[BCR] Install Carrier : {0}", carrierID );

                    if ( string.IsNullOrEmpty(carrierID) )
                    {
                        logger.W( "[BCR] Carrier is null or Empty : {0}", u.ID );
                        carrierID = string.Format( "UNKNOWN-{0}", DateTime.Now.ToString( "yyyyMMddHHmmssff" ) );

                        plc.WriteBit( u.ID + "#BCRMISMATCH", true );
                    }
                    this.InstallCarrier( carrierID, u.ID, E_IDREAD_STATUS.SUCCESS );
                }
            }
            //자재정보 변경되면 Web에 알려줘야, 반영됨.
            this.SendUnitStatusToWeb( u.ID );
        }

        void IDReadFail( TcsUnit u, string carrierID )
        {
            bool isCarrier = sql.Carrier.HasByUnit( u.ID );

            if ( isCarrier )
            {
                //자재가 있으면 Remove 처리먼저 한다.
                TcsCarrier carrier = sql.Carrier.GetCarrierByUnit( u.ID );
                this.RemoveCarrier( carrier.ID, u.ID, E_IDREAD_STATUS.FAIL );
                LockUtils.Wait( 300 );
            }

            //UNK Install 처리한다.
            string unkID = string.Format( "UNKNOWN{0}{1}", u.MachineID, DateTime.Now.ToString( "yyyyMMddHHmmssff" ) );
            this.InstallCarrier( unkID, u.ID, E_IDREAD_STATUS.FAIL );

            //2017.07.13 Toptec Kang.
            plc.WriteBit( u.ID + "#BCRMISMATCH", true );

            //자재정보 변경되면 Web에 알려줘야, 반영됨.
            this.SendUnitStatusToWeb( u.ID );
        }
    }
}
