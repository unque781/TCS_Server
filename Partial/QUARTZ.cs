using System;
using System.Collections.Generic;
using Floware.Concurrent;
using Floware.MQ;
using Floware.OSView;
using Floware.Quartz;
using Floware.Utils;
using TcsV2.Core.DTO;
using TcsV2.Core.MODEL;
using Floware.FileSystem;
using System.IO;
using TcsV2.Core;


namespace TCS.ServerV2
{

    partial class FormMain
    {
        private void InitQuartz()
        {
            this.QuzScheduleTransfer();
            this.QuzMachineState();

            QuzOnPcUsage();
            QuzOnBackupMySQL();
        }

        private void StopQuartz()
        {
            QuartzUtils.StopSchedule("MACHINESTATE");
            QuartzUtils.StopSchedule("SCHEDULE");
            QuartzUtils.StopSchedule(E_APP_CONFIG.PARAM_PC_USAGE.ToString());
            QuartzUtils.StopSchedule(E_APP_CONFIG.PARAM_BACKUP_DB.ToString());            
        }

        void QuzMachineState()
        {
            QuartzUtils.Invoke("MACHINESTATE", QuartzUtils.GetExpnSecond(30), RefMachine);
        }

        public void QuzScheduleTransfer()
        {
            QuartzUtils.Invoke("SCHEDULE", "1/5 * * * * ?", HandleScheduleTransfer);
        }

        //Winform 화면 표시.
        public void DspWinFormTransfer()
        {
            try
            {
                this.tssWaitTr.Text = sql.Command.CountForWait().ToString();
                this.tssRunTr.Text = sql.Command.CounitForRunning().ToString();
            }
            catch (Exception er)
            {
                logger.E("[QUARTZ] " + er.Message);
            }
        }

        void HandleWebQuartzChange(MQMessage mqmsg)
        {
            string[] tmpMsg = mqmsg.GetFieldV("MSG").Split('|');

            WebQuartzDto q = new WebQuartzDto
            {
                Name = (E_APP_CONFIG)Enum.Parse(typeof(E_APP_CONFIG), tmpMsg[0]),
                Stop = bool.Parse(tmpMsg[1]),
                QType = (WebQuartzDto.QuzType)Enum.Parse(typeof(WebQuartzDto.QuzType), tmpMsg[2]),
                WebLog = tmpMsg[3],
                UserID = tmpMsg[4],
                IPAddr = tmpMsg[5],
            };

            try
            {
                logger.I("[QUARTZ] " + q.ToString());
                QuartzUtils.StopSchedule(q.Name.ToString());

                var u = sql.User[q.UserID];

                var v = new TcsUserHisAction
                {
                    ActTime = DateTime.Now,
                    Content = q.WebLog,
                    ID = Guid.NewGuid().ToString(),
                    UserID = u.ID,
                    IpAddr = q.IPAddr,
                    Name = u.Name,
                };
                sql.HisUserAction.AddHisAction(v);

                if (q.Stop)
                    return;

                switch (q.Name)
                {
                    case E_APP_CONFIG.PARAM_BACKUP_DB:
                        QuartzUtils.Invoke(q.Name.ToString(), q.CronExp, BackupMySQL);
                        break;

                    case E_APP_CONFIG.PARAM_PC_USAGE:
                        QuartzUtils.Invoke(q.Name.ToString(), q.CronExp, CollectPcUsage);
                        break;
                }

                this.ReplyToWeb(0, mqmsg);

            }
            catch (Exception e)
            {
                logger.E("[QUARTZ] " + e.Message);
                this.ReplyToWeb(2, mqmsg);
            }
        }

        void CollectPcUsage()
        {
            //현재 System 값을 구한다.
            var nSys = new TcsHisSystem
            {
                Cpu = Convert.ToInt32(Management.CpuUseRate()),
                Mem = Convert.ToInt32(Management.MemPhysicalUseRate()),
            };

            var ll = Management.HddList();
            nSys.DiskFreeC = (int)(ll[0].AvailableFreeSpace / ConstUtils.ONE_GIGA_BYTES);
            nSys.DiskFreeD = (int)(ll[1].AvailableFreeSpace / ConstUtils.ONE_GIGA_BYTES);

            //History 에 ADD
            this.sql.HisSystem.AddN(nSys);

            //History Clean
            this.sql.HisSystem.Clean();

            //System 기준정보를 가져온다.
            var sys = sql.System.Get();

            //5분간 평균 System 값을 가져온다.
            var avgSys = sql.HisSystem.GetAvg(sys.MonitoringTime);

            sys.Cpu = nSys.Cpu;
            sys.Mem = nSys.Mem;
            sys.DiskFreeC = nSys.DiskFreeC;
            sys.DiskFreeD = nSys.DiskFreeD;

            int memLevel = 0;
            int cpuLevel = 0;
            if (sys.Cpu1Limit < avgSys.Cpu) cpuLevel = 1;
            if (sys.Cpu2Limit < avgSys.Cpu) cpuLevel = 2;
            if (sys.Cpu3Limit < avgSys.Cpu) cpuLevel = 3;

            if (sys.Mem1Limit < avgSys.Mem) memLevel = 1;
            if (sys.Mem2Limit < avgSys.Mem) memLevel = 2;
            if (sys.Mem3Limit < avgSys.Mem) memLevel = 3;

            //Cpu/Mem 중 Level 큰값을 저장한다.
            sys.Level = Math.Max(cpuLevel, memLevel);

            sys.EventTime = DateTime.Now;
            this.sql.System.ChgSystem(sys);

            logPc.I(sys);

            if (sys.Level == 3)
            {
                logger.W("[QUARTZ] system CPU or Memory Level=3");
                File.WriteAllText(sql.AppConfig.PrefSystem.Value, "FailOverRequest=1");
            }
            else
            {
                File.WriteAllText(sql.AppConfig.PrefSystem.Value, "FailOverRequest=0");
            }

            SendToWeb(sys.PushMsg);
        }

        void QuzOnPcUsage()
        {
            int i = sql.AppConfig[E_APP_CONFIG.PARAM_PC_USAGE].IntValue;
            string exp = new WebQuartzDto { Interval = i, QType = WebQuartzDto.QuzType.MINUTE }.CronExp;//string.Format("0 0/{0} * * * ?", i);

            QuartzUtils.Invoke(E_APP_CONFIG.PARAM_PC_USAGE.ToString(), exp, CollectPcUsage);
        }

        void QuzOnBackupMySQL()
        {
            int i = sql.AppConfig[E_APP_CONFIG.PARAM_BACKUP_DB].IntValue;
            if (i < 1)
                return;

            string exp = new WebQuartzDto { Interval = i, QType = WebQuartzDto.QuzType.DAY }.CronExp;//string.Format("0 0 12 1/{0} * ?", i);
            QuartzUtils.Invoke(E_APP_CONFIG.PARAM_BACKUP_DB.ToString(), exp, BackupMySQL);
        }

        void BackupMySQL()
        {
                string exe = @"D:\TCS\BACKUP\TcsDump.bat";
                string wd = @"D:\TCS\BACKUP";
                ProcessUtils.Start(exe, wd, string.Empty);
                logger.I("[QUARTZ] Backup DB");
        }

    }
}
