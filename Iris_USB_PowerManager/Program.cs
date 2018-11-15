using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;
using Microsoft.Win32.TaskScheduler;
using System.IO;

namespace Iris_USB_PowerManager
{
    class Program
    {
        static void Main(string[] args)
        {
 

            try
            {
                BuildAsATask();
                ManagementObjectSearcher PMSearcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM MSPower_DeviceEnable");
                ManagementObjectSearcher HubSearcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_USBHub");
                foreach (ManagementObject PMQueryObj in PMSearcher.Get())
                {
                    string s_ControllerName = PMQueryObj["InstanceName"].ToString().ToUpper();
                    foreach (ManagementObject HubQueryObj in HubSearcher.Get())
                    {
                        string PnpDeviceID = HubQueryObj["PNPDeviceID"].ToString();
                        if (s_ControllerName.Contains(PnpDeviceID))
                        {
                            PMQueryObj.SetPropertyValue("Enable", "False");
                            PMQueryObj.Put();
                        }
                    }
                }
                RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Services\USB");
                key.SetValue("DisableSelectiveSuspend", 1, RegistryValueKind.DWord);
                
                //disable selective suspend on battery and AC power
                var guidPlans = GetAll();   
                foreach (Guid guidPlan in guidPlans)
                {
                    Process p = new Process();
                    p.StartInfo.FileName = "powercfg.exe";
                    p.StartInfo.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.Arguments = @"/setacvalueindex " + guidPlan + " 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0";
                    p.Start();
                    p.WaitForExit();

                    Process p2 = new Process();
                    p2.StartInfo.FileName = "powercfg.exe";
                    p2.StartInfo.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
                    p2.StartInfo.UseShellExecute = false;
                    p2.StartInfo.Arguments = @"/setdcvalueindex " + guidPlan + " 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0";
                    p2.Start();
                    p2.WaitForExit();
                }

                //Console.WriteLine("Successfully Disabled All Power Manamgement Settings.");
            }
            catch (Exception ex)
            {
                //Console.WriteLine("Unable to make modifications, Please Run this Application as Administrator");
            }
        }

        private static string GetCurrentPowerScheme()
        {
            IntPtr ptrActiveGuid = IntPtr.Zero;
            uint res = PowerGetActiveScheme(IntPtr.Zero, ref ptrActiveGuid);
            if (res == 0)
            {
                uint buffSize = 0;
                res = PowerReadFriendlyName(IntPtr.Zero, ptrActiveGuid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ref buffSize);
                if (res == 0)
                {
                    IntPtr ptrName = Marshal.AllocHGlobal((int)buffSize);
                    res = PowerReadFriendlyName(IntPtr.Zero, ptrActiveGuid, IntPtr.Zero, IntPtr.Zero, ptrName, ref buffSize);
                    if (res == 0)
                    {
                        string ret = Marshal.PtrToStringUni(ptrName);
                        Marshal.FreeHGlobal(ptrName);
                        return ret;
                    }
                    Marshal.FreeHGlobal(ptrName);
                }
            }
            throw new Exception("Error reading current power scheme. Native Win32 error code = " + res);
        }


        [DllImport("PowrProf.dll")]
        public static extern UInt32 PowerEnumerate(IntPtr RootPowerKey, IntPtr SchemeGuid, IntPtr SubGroupOfPowerSettingGuid, UInt32 AcessFlags, UInt32 Index, ref Guid Buffer, ref UInt32 BufferSize);

        [DllImport("PowrProf.dll")]
        public static extern UInt32 PowerReadFriendlyName(IntPtr RootPowerKey, ref Guid SchemeGuid, IntPtr SubGroupOfPowerSettingGuid, IntPtr PowerSettingGuid, IntPtr Buffer, ref UInt32 BufferSize);

        [DllImport("powrprof.dll")]
        public static extern UInt32 PowerReadFriendlyName(
        IntPtr RootPowerKey,IntPtr SchemeGuid,IntPtr SubGroupOfPowerSettingGuid,IntPtr PowerSettingGuid,IntPtr Buffer, ref UInt32 BufferSize);

        [DllImport("powrprof.dll")]
        public static extern UInt32 PowerGetActiveScheme(IntPtr UserRootPowerKey, ref IntPtr ActivePolicyGuid);


        public enum AccessFlags : uint
        {
            ACCESS_SCHEME = 16,
            ACCESS_SUBGROUP = 17,
            ACCESS_INDIVIDUAL_SETTING = 18
        }

        private static string ReadFriendlyName(Guid schemeGuid)
        {
            uint sizeName = 1024;
            IntPtr pSizeName = Marshal.AllocHGlobal((int)sizeName);

            string friendlyName;

            try
            {
                PowerReadFriendlyName(IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, pSizeName, ref sizeName);
                friendlyName = Marshal.PtrToStringUni(pSizeName);
            }
            finally
            {
                Marshal.FreeHGlobal(pSizeName);
            }

            return friendlyName;
        }

        public static IEnumerable<Guid> GetAll()
        {
            var schemeGuid = Guid.Empty;

            uint sizeSchemeGuid = (uint)Marshal.SizeOf(typeof(Guid));
            uint schemeIndex = 0;

            while (PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, (uint)AccessFlags.ACCESS_SCHEME, schemeIndex, ref schemeGuid, ref sizeSchemeGuid) == 0)
            {
                yield return schemeGuid;
                schemeIndex++;
            }
        }
        public static void BuildAsATask()
        {
            using (TaskService ts = new TaskService())
            {
                TaskDefinition td = ts.NewTask();
                td.RegistrationInfo.Description = "Digital Doc - Disables the Power Management settings for the Computers USB Controllers and Disables Selective Suspend for the Power Schemes.\r\n C# Application by James Petko(jpetko@digi-doc.com)";
                td.Triggers.Add(new LogonTrigger());
                td.Principal.RunLevel = TaskRunLevel.Highest;
                td.Actions.Add(new ExecAction(Directory.GetCurrentDirectory() + "\\" + "Iris_USB_PowerManager.exe"));
                ts.RootFolder.RegisterTaskDefinition(@"USB Power Manager", td);
            }
        }
    }
}
