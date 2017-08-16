using System;
using System.Windows.Forms;
using Floware.MQ;

using TcsV2.Core;
using TcsV2.Core.DTO;
using TcsV2.Core.MODEL;
using System.Threading;

namespace TCS.ServerV2
{

    public partial class FormTest : Form
    {

        public SqlManager sql { get; set; }
        public MQClient mq { get; set; }

        public FormTest()
        {
            InitializeComponent();
        }

        private void bVehicleRemove_Click(object sender, EventArgs e)
        {
            //WebVehicleInstallDto o = new WebVehicleInstallDto();
            //o.ActionType =TcsV2.Core.E_VEHICLE_INST_STATE.REMOVE;
            //o.VehicleID = "MCFB102";

            //FmqUtils.ReqReply<WebVehicleInstallDto>(mq, o, "FMQ.TCS.SERVER", 1000);
        }

        private void bVehicleInstall_Click(object sender, EventArgs e)
        {
            //WebVehicleInstallDto o = new WebVehicleInstallDto();
            //o.ActionType = TcsV2.Core.E_VEHICLE_INST_STATE.INSTALL;
            //o.VehicleID = "MCFB102";

            //FmqUtils.ReqReply<WebVehicleInstallDto>(mq, o, "FMQ.TCS.SERVER", 1000);
        }

        private void FormTest_Load(object sender, EventArgs e)
        {
            var ll = sql.Unit.All;

            foreach (var item in ll)
            {
                if (item.IsVehicle)
                {
                    cbVehicle.Items.Add(item.ID);
                }
                else
                {
                    cbUnit.Items.Add(item.ID);

                }
            }
            
            var mll = sql.Machine.All;

            foreach (var item in mll)
            {
                cbMachine.Items.Add(item.ID);

            }

            tbCarrerId.Text = "CARRIER" + new Random().Next(100, 999);
        }

        private void bPMSet_Click(object sender, EventArgs e)
        {
            //bool hasUnit = sql.Unit.Has(cbUnit.Text);

            //var o = new WebPMDto();
            //o.ActionType = WebPMDto.Action.PMSETTING;
            //o.UnitID = hasUnit ? cbUnit.Text : cbVehicle.Text;
            //o.UnitType = hasUnit ? WebPMDto.UType.UNIT : WebPMDto.UType.VEHICLE;

            //FmqUtils.ReqReply<WebPMDto>(mq, o, "FMQ.TCS.SERVER", 1000);
        }

        private void bBMClear_Click(object sender, EventArgs e)
        {
            //bool hasUnit = sql.Unit.Has(cbUnit.Text);

            //var o = new WebPMDto();
            //o.ActionType = WebPMDto.Action.BMCLEAR;
            //o.UnitID = hasUnit ? cbUnit.Text : cbVehicle.Text;
            //o.UnitType = hasUnit ? WebPMDto.UType.UNIT : WebPMDto.UType.VEHICLE;

            //FmqUtils.ReqReply<WebPMDto>(mq, o, "FMQ.TCS.SERVER", 1000);
        }

        private void bBMSet_Click(object sender, EventArgs e)
        {
            //bool hasUnit = sql.Unit.Has(cbUnit.Text);

            //var o = new WebPMDto();
            //o.ActionType = WebPMDto.Action.BMSETTING;
            //o.UnitType = hasUnit ? WebPMDto.UType.UNIT : WebPMDto.UType.VEHICLE;
            //o.UnitID = hasUnit ? cbUnit.Text : cbVehicle.Text;

            //FmqUtils.ReqReply<WebPMDto>(mq, o, "FMQ.TCS.SERVER", 1000);
        }

        private void bPMClear_Click(object sender, EventArgs e)
        {
            //bool hasUnit = sql.Unit.Has(cbUnit.Text);

            //var o = new WebPMDto();
            //o.ActionType = WebPMDto.Action.PMCLEAR;
            //o.UnitType = hasUnit ? WebPMDto.UType.UNIT : WebPMDto.UType.VEHICLE;
            //o.UnitID = hasUnit ? cbUnit.Text : cbVehicle.Text;

            //FmqUtils.ReqReply<WebPMDto>(mq, o, "FMQ.TCS.SERVER", 1000);
        }

        public void btnCimCarrierSend1_Click(object sender, EventArgs e)
        {
            //bool hasUnit = sql.Unit.Has(cbUnit.Text);
            //bool hasMachine = sql.Machine.Has(cbMachine.Text);

            //if (!hasUnit && !hasMachine)
            //{
            //    MessageBox.Show("Machine 또는 Unit 을 선택하세요.");
            //    return;
            //}

            //var o = new WebCarrierDto();
            //o.ActionType = WebCarrierDto.Action.ADD;
            //o.Carrier = new TcsCarrier
            //{
            //    ID = tbCarrerId.Text,
            //    CommandID = string.Empty,
            //    CurrUnitID = hasUnit ? cbUnit.Text : cbMachine.Text,
            //    CleanState = TcsCarrier.CLEAN_STATE.CLEAN,
            //};
            
            ////o.UnitType = hasUnit ? WebCarrierDto.UType.UNIT : WebCarrierDto.UType.MACHINE;

            //FmqUtils.ReqReply<WebCarrierDto>(mq, o, "FMQ.TCS.SERVER", 1000);

            //Thread.Sleep(1000);
        }
    }
}
