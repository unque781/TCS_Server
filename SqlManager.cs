using Floware.SQL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TcsV2.Core.DAO;


namespace TCS.ServerV2
{

    public class SqlManager
    {

        public NativeSqlSupport Nss { get; set; }
        public TcsAlarmDao Alarm { get; set; }
        public TcsAlarmDefineDao AlarmDef { get; set; }
        public TcsHisAlarmDao HisAlarm { get; set; }

        public TcsHisHostNakDao HostNak { get; set; }
        public TcsHisPcUsageDao PcUsage { get; set; }
        public TcsHisSystemDao HisSystem { get; set; }
        public TcsSystemDao System { get; set; }

        public TcsAppConfigDao AppConfig { get; set; }
        public TcsCarrierDao Carrier { get; set; }
        public TcsCommandDao Command { get; set; }
        public TcsSubCmdDao SubCmd { get; set; }
        public TcsHisCommandDao HisCommand { get; set; }
        public TcsIODao IO { get; set; }
        public TcsIODefineDao IODef { get; set; }
        public TcsMachineDao Machine { get; set; }
        public TcsUnitDao Unit { get; set; }

        public TcsHisUserActionDao HisUserAction { get; set; }
        public TcsHisUserLogonDao HisUserLogon { get; set; }

        public TcsUserDao User { get; set; }
        public TcsHsmsConfigDao HsmsConfig { get; set; }

        public TcsHsmsEventDao HsmsEvent { get; set; }
        public TcsCommandChgDao CommandChg { get; set; }

        public TcsCycleConfigDao CycleConfig { get; set; }
        public TcsRouteDao Route { get; set; }

        //public TcsLogVRPDao LogVRP { get; set; }
        //public TcsLogWarnDao LogWarn { get; set; }

    }
}
