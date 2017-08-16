using System;
using System.Windows.Forms;
using Floware.Concurrent;
using Floware.Logging;
using Floware.Spring;
using Floware.Spring.Context;
using Floware.Utils;


namespace TCS.ServerV2
{

    static class Program
    {

        static Logger logger = Logger.GetLogger();

        [STAThread]
        static void Main(string[] args)
        {

            string logpath = string.Format("Config/log4net-{0}.xml", args[0]);
            LogUtils.Configure(logpath);

            try
            {
                AppUtils.LogGlobalException();
                SpringUtils.AddDbProviderResource();

                logger.I("{0} Started {0}", string.Empty.PadRight(40, '+'));
                SpringUtils.AddDbProviderResource();

                ThreadUtils.InitPoolSize(5);


                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppContextXmlFile context = new AppContextXmlFile("Config/spring.xml");

                var form = context["formMain"] as FormMain;
                form.Arg = args[0];
                Application.Run(form);

            }
            catch (Exception e)
            {
                logger.E(e);
            }
            logger.I("{0} Stopped {0}", string.Empty.PadRight(40, '-'));

        }
    }
}
