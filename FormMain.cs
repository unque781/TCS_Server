using System;
using System.Linq;
using System.Windows.Forms;
using Floware.Concurrent;
using Floware.LINQ;
using Floware.Logging;
using Floware.Quartz;
using Floware.Utils;
using TcsV2.Core;
using TcsV2.Core.MODEL;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;

namespace TCS.ServerV2
{

    public partial class FormMain : Form
    {
        static Logger logger = Logger.GetLogger();
        static Logger logPc = Logger.GetLogger("PC_USAGE");

        public int WAIT_VRP_OHS { get; set; }
        public int WAIT_VRP_VEHICLE { get; set; }
        public int WAIT_VRP_MOVEREQ { get; set; }
        public int WAIT_ALTERNATEUNIT_CMD { get; set; }

        public SqlManager sql { get; set; }
        public string Arg { get; set; }
        public bool SERVER_ACTIVE { get; set; } //FMQ Active 상태인지 확인.
        public bool INITSTART { get; set; }     //최초 Load 되었는지 확인용

        const string VER_SERVER = "170310";

        public FormMain()
        {
            InitializeComponent();
        }

        List<TcsUnit> allVehicle = new List<TcsUnit>();
        private void FormMain_Load(object sender, EventArgs e)
        {
            var processes = Process.GetProcessesByName("TCS.ServerV2");

            if(processes.Count() > 1)
            {
                MessageBox.Show("Running TCS Server");
                Process.GetCurrentProcess().Kill();
            }
            sql.Nss.TableAdd(typeof(TcsCommand));
            xls.InitXls("Config/Map1.8.xlsx");
            
            sql.HsmsConfig.ChgControlMode(E_CONTROLSTATE.ONLINE_LOCAL);
            sql.HsmsConfig.ChgControlState(E_CTL_STATE.PAUSED);
            

            SERVER_ACTIVE = false;
            INITSTART = true;

            notifyIcon.Text = Text += string.Format(" v{0} {1:MM-dd HH:mm:ss} -{2}", VER_SERVER, DateTime.Now, Arg);
            allVehicle = sql.Unit.AllVehicle();

            dg1.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dg1.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dg1.ColumnCount = 3;
            dg1.Columns[0].HeaderText = "ID";
            dg1.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dg1.Columns[1].HeaderText = "COMM";
            dg1.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dg1.Columns[2].HeaderText = "State";
            dg1.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            sql.Nss.TableChk("TcsV2.Core");

            try
            {
                QuartzUtils.Init(5);

                this.InitMQ();
                this.InitPLC();
                this.InitHsms();
                this.InitQuartz();
                this.ScreenLog("TCS Server START");
                this.allVehicle.FwEach(x => ThreadUtils.InvokeEx(InitVrp, x.ID));

                ThreadUtils.InvokeEx(InitVsp);
                ThreadUtils.InvokeEx(InitCycleCmd);
                SendStateToWeb();
            }
            catch (Exception er)
            {
                logger.E(er);
            }
        }

        void RefMachine()
        {
            var ll = sql.Machine.All;
            dg1.Rows.Clear();
            ll.Select(x => new object[] { x.ID, x.ConnState, x.State,  }).FwEach(x => dg1.Rows.Add(x));
        }

        #region screen log
        void ScreenLog(string msg)
        {
            DlgUtils.Invoke(WriteScreenLog, msg);
        }

        void WriteScreenLog(string log)
        {
            if (this.txtLog.TextLength > 2000) this.txtLog.Clear();

            string text = string.Format("{0} : {1}{2}", DateTime.Now.ToString("MM/dd HH:mm:ss:fff"), log, Environment.NewLine);
            this.txtLog.AppendText(text);

            txtLog.Select(txtLog.Text.Length, 0);
            txtLog.ScrollToCaret();
        }
        #endregion


        #region TrayIcon
        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            this.Visible = true;
            this.ShowInTaskbar = true;
            this.WindowState = FormWindowState.Normal;
            notifyIcon.Visible = false;
        }

        private void FormMain_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                this.Visible = false;
                notifyIcon.Visible = true;
            }
        }

        private void mnuShow_Click(object sender, EventArgs e)
        {
            this.Visible = true;
            notifyIcon.Visible = false;
            this.WindowState = FormWindowState.Normal;
        }

        private void mnuExit_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            DialogResult result = MessageBox.Show("TCS.Server Process down?", "Information", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == System.Windows.Forms.DialogResult.Yes)
            {
                sql.HsmsConfig.ChgConnectState(E_CONN_STATE.DIS_CONNECT);
                sql.HsmsConfig.ChgControlState(E_CTL_STATE.PAUSED);
                sql.HsmsConfig.ChgControlMode(E_CONTROLSTATE.OFFLINE);
                this.SendStateToWeb();
                return;
            }

            e.Cancel = true;
        }
        #endregion

        private void tssClear_Click(object sender, EventArgs e)
        {
            this.txtLog.Clear();
        }

        private void tssLog_Click(object sender, EventArgs e)
        {
            string myPath = @"C:\Log\TCS";
            System.Diagnostics.Process prc = new System.Diagnostics.Process();
            prc.StartInfo.FileName = myPath;
            prc.Start();
        }

        private void DspFmqState()
        {
            this.tssActive.Text = this.SERVER_ACTIVE ? "ACTIVE" : "DEACTIVE";
        }

        private void toolStripBtnTest_Click(object sender, EventArgs e)
        {
            new FormTest { sql = sql, mq = mq }.Show(this);
        }
    }
}

