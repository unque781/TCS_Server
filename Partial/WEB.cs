using Floware.MQ;
using TcsV2.Core.DTO;
using TcsV2.Core.MODEL;
using Floware.Quartz;
using System.Linq;
using TcsV2.Core;

namespace TCS.ServerV2
{

    partial class FormMain
    {

        
        void SendAlarmToWeb()
        {
            int cnt = this.sql.Alarm.Count();
            this.SendToWeb(new WebAlarmDto() { AlarmCount = cnt }.PushMsg);
        }

        void SendCommandToWeb()
        {
            //int cnt = this.sql.Command.Count();
            this.SendToWeb(new WebCommandDto() { TransferMsg = sql.Command.CmdCountSummary() }.PushMsg);
        }

        void SendVehicleLocationToWeb(string vehicleID, int stationID)
        {
            var v = sql.Unit[vehicleID];
            string vehicleLoc = string.Format("{0}_{1}", v.MachineID, stationID);

            this.SendToWeb(new WebVehicleLocDto(v.ID, vehicleLoc).PushMsg);
        }

        void SendVehicleStateToWeb(string vehicleID)
        {
            TcsUnit v = sql.Unit[vehicleID];
            this.SendToWeb(new WebVehicleStateDto(v.ID, v.State, v.Status, sql.Carrier.ListN(x=>x.CurrUnitID == v.ID)).PushMsg);
        }

        void SendVehicleInstallToWeb(string vehicleID)
        {
            TcsUnit v = sql.Unit[vehicleID];
            this.SendToWeb(new WebVehicleInstallDto(v.ID, v.InstallState, v.State, v.Status, sql.Carrier.ListN(x=>x.CurrUnitID == v.ID)).PushMsg);
        }

        void SendVehicleSubStateToWeb(string vehicleID)
        {
            SendVehicleSubStateToWeb(vehicleID, string.Empty);
        }

        void SendVehicleSubStateToWeb(string vehicleID, string unitID)
        {
            TcsUnit v = sql.Unit[vehicleID];
            var c = sql.Carrier.ListN(x => x.CurrUnitID == v.ID);
            var dto = new WebVehicleSubStateDto { Vehicle = v, CarrierList = c };
            this.SendToWeb(dto.PushMsg);
        }

        void SendVehicleStatusToWeb(string vehicleID)
        {
            TcsUnit v = sql.Unit[vehicleID];
            this.SendToWeb(new WebVehicleStatusDto(v).PushMsg);
        }

        void SendMachineUpdateToWeb(string machineID)
        {
            this.SendToWeb(new WebMachineUpdateDto(machineID).PushMsg);
        }

        void SendMachineStateToWeb(string machineID)
        {
            TcsMachine m = sql.Machine[machineID];
            this.SendToWeb(new WebMachineStateDto(m.ID, m).PushMsg);
        }

        void SendUnitStateToWeb(string unitID)
        {
            TcsUnit u = sql.Unit[unitID];
            var c = sql.Carrier.ListN(x => x.CurrUnitID == u.ID);
            this.SendToWeb(new WebUnitStateDto(u.ID, u.State, u.Status, c).PushMsg);
        }

        void SendUnitStatusToWeb(string unitID)
        {
            TcsUnit u = sql.Unit[unitID];
            var c = sql.Carrier.ListN(x => x.CurrUnitID == u.ID);
            this.SendToWeb(new WebUnitStatusDto(u.ID, u.Status, u.State, c).PushMsg);
        }

        private void SendDriectionToWeb(string soc, string dest)
        {
            this.SendToWeb(new WebUnitDriectionDto(soc, dest).PushMsg);
        }

        private void ReplyToWeb(int nakCode, MQMessage req)
        {
            MQMessage reply = new MQMessage();
            reply.ToName = sql.AppConfig.FmqWebSubject.Value;
            reply.AddField("NakCode", nakCode);
            //reply.Serialize(o);

            if (req.IsRequest)
                this.mq.Reply(req, reply);
        }

        private void ReplyToWeb( MQMessage reply, MQMessage req )
        {
            reply.ToName = sql.AppConfig.FmqWebSubject.Value;

            if ( req.IsRequest )
                this.mq.Reply( req, reply );
        }

        void ReplyToWeb(object o, MQMessage req)
        {
            MQMessage reply = new MQMessage();
            reply.ToName = sql.AppConfig.FmqWebSubject.Value;
            reply.SetValue(o.ToString());
            //reply.Serialize(o);

            if(req.IsRequest)
                this.mq.Reply(req, reply);
        }

        void ReplyToWeb(object o, string reqName)
        {
            MQMessage reply = new MQMessage();
            reply.ToName = sql.AppConfig.FmqWebSubject.Value;
            reply.Serialize(o);

            if (!string.IsNullOrEmpty(reqName))
                this.mq.Reply(reqName, reply);
        }

        void SendToWeb(object o)
        {
            MQMessage msg = new MQMessage();
            msg.ToName = sql.AppConfig.FmqWebSubject.Value;
            msg.SetValue(o.ToString());

            //msg.Serialize(o);

            this.mq.Send(msg);
        }


        private void SendStateToWeb()
        {
            WebHsmsStateDto dto = new WebHsmsStateDto();
            
            dto.ControlMode = sql.HsmsConfig.ControlMode.Value;
            dto.ControlState = sql.HsmsConfig.ControlState.Value;
            dto.CommState = sql.HsmsConfig.ConnectState.Value;
            SendToWeb(dto.PushMsg);
        }
    }
}
