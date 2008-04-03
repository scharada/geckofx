#region ***** BEGIN LICENSE BLOCK *****
/* Version: MPL 1.1/GPL 2.0/LGPL 2.1
 *
 * The contents of this file are subject to the Mozilla Public License Version
 * 1.1 (the "License"); you may not use this file except in compliance with
 * the License. You may obtain a copy of the License at
 * http://www.mozilla.org/MPL/
 *
 * Software distributed under the License is distributed on an "AS IS" basis,
 * WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License
 * for the specific language governing rights and limitations under the
 * License.
 *
 * The Original Code is Skybound Software code.
 *
 * The Initial Developer of the Original Code is Skybound Software.
 * Portions created by the Initial Developer are Copyright (C) 2008
 * the Initial Developer. All Rights Reserved.
 *
 * Contributor(s):
 *
 * Alternatively, the contents of this file may be used under the terms of
 * either the GNU General Public License Version 2 or later (the "GPL"), or
 * the GNU Lesser General Public License Version 2.1 or later (the "LGPL"),
 * in which case the provisions of the GPL or the LGPL are applicable instead
 * of those above. If you wish to allow use of your version of this file only
 * under the terms of either the GPL or the LGPL, and not to allow others to
 * use your version of this file under the terms of the MPL, indicate your
 * decision by deleting the provisions above and replace them with the notice
 * and other provisions required by the GPL or the LGPL. If you do not delete
 * the provisions above, a recipient may use your version of this file under
 * the terms of any one of the MPL, the GPL or the LGPL.
 */
#endregion END LICENSE BLOCK

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Forms;

namespace Skybound.Gecko
{
	/// <summary>
	/// Provides low-level access to XPCOM.
	/// </summary>
	public static class Xpcom
	{
		#region Native Methods
		[DllImport("xpcom", CharSet = CharSet.Ansi)]
		static extern int NS_InitXPCOM2(out IntPtr serviceManager, [MarshalAs(UnmanagedType.IUnknown)] object binDirectory, nsIDirectoryServiceProvider appFileLocationProvider);
		
		[DllImport("xpcom", CharSet = CharSet.Ansi)]
		static extern int NS_NewNativeLocalFile(nsACString path, bool followLinks, [MarshalAs(UnmanagedType.IUnknown)] out object result);
		
		[DllImport("xpcom", CharSet = CharSet.Ansi)]
		static extern int NS_GetComponentManager(out nsInterfaces componentManager);
		
		[DllImport("xpcom", CharSet = CharSet.Ansi)]
		static extern int NS_GetComponentRegistrar(out nsIComponentRegistrar componentRegistrar);
		
		[DllImport("xpcom", EntryPoint="NS_Alloc")]
		public static extern IntPtr Alloc(int size);
		
		[DllImport("xpcom", EntryPoint="NS_Realloc")]
		public static extern IntPtr Realloc(IntPtr ptr, int size);
		
		[DllImport("xpcom", EntryPoint="NS_Free")]
		public static extern void Free(IntPtr ptr);
		#endregion
		
		/// <summary>
		/// Initializes XPCOM using the current directory as the XPCOM directory.
		/// </summary>
		public static void Initialize()
		{
			Initialize(null);
		}
		
		/// <summary>
		/// Initializes XPCOM using the specified directory.
		/// </summary>
		/// <param name="binDirectory">The directory which contains xpcom.dll.</param>
		public static void Initialize(string binDirectory)
		{
			if (_IsInitialized)
				return;
			
			string folder = binDirectory ?? Environment.CurrentDirectory;
			string xpcomPath = Path.Combine(folder, "xpcom.dll");
			
			if (Debugger.IsAttached)
			{
				// make sure this DLL is there
				if (!File.Exists(xpcomPath))
				{
					if (MessageBox.Show("Couldn't find XULRunner in '" + folder + "'.  Call Xpcom.Initialize() in your application startup code and specify the directory where XULRunner is installed.\r\n\r\n" +
						"If you do not have XULRunner installed, click Yes to open the download page.  Otherwise, click No, and update your application startup code.",
							"XULRunner Not Found", MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes)
					{
						Process.Start("http://releases.mozilla.org/pub/mozilla.org/xulrunner/releases/1.8.1.3/contrib/win32");
					}
					
					Environment.Exit(0);
				}
			}
			
			if (binDirectory != null)
			{
				Environment.SetEnvironmentVariable("path",
					Environment.GetEnvironmentVariable("path") + ";" + binDirectory, EnvironmentVariableTarget.Process);
			}
			
			object mreAppDir = null;
			
			if (binDirectory != null)
			{
				using (nsACString str = new nsACString(Path.GetFullPath(binDirectory)))
					if (NS_NewNativeLocalFile(str, true, out mreAppDir) != 0)
					{
						throw new Exception("Failed on NS_NewNativeLocalFile");
					}
			}
			
			// temporarily change the current directory so NS_InitEmbedding can find all the DLLs it needs
			String oldCurrent = Environment.CurrentDirectory;
			Environment.CurrentDirectory = folder;
			
			IntPtr serviceManagerPtr;
			//int res = NS_InitXPCOM2(out serviceManagerPtr, mreAppDir, new DirectoryServiceProvider());
			int res = NS_InitXPCOM2(out serviceManagerPtr, mreAppDir, null);
			
			// change back
			Environment.CurrentDirectory = oldCurrent;
			
			if (res != 0)
			{
				throw new Exception("Failed on NS_InitXPCOM2");
			}
			
			ServiceManager = (nsIServiceManager)Marshal.GetObjectForIUnknown(serviceManagerPtr);
			
			// get some global objects we will need later
			NS_GetComponentManager(out ComponentManager);
			NS_GetComponentRegistrar(out ComponentRegistrar);
			
			_IsInitialized = true;
		}
		
		static bool _IsInitialized;
		
		static nsInterfaces ComponentManager;
		static nsIComponentRegistrar ComponentRegistrar;
		static nsIServiceManager ServiceManager;
		
		public static object NewNativeLocalFile(string filename)
		{
			object result;
			
			using (nsACString str = new nsACString(filename))
				if (NS_NewNativeLocalFile(str, true, out result) == 0)
					return result;
			
			return null;
		}
		
		public static object CreateInstance(Guid classIID)
		{
			Guid iid = typeof(nsISupports).GUID;
			return ComponentManager.CreateInstance(ref classIID, null, ref iid);
		}
		
		public static object CreateInstance(string contractID)
		{
			return CreateInstance<nsISupports>(contractID);
		}
		
		public static TInterfaceType CreateInstance<TInterfaceType>(string contractID)
		{
			Guid iid = typeof(TInterfaceType).GUID;
			return (TInterfaceType)ComponentManager.CreateInstanceByContractID(contractID, null, ref iid);
		}
		
		public static TInterfaceType QueryInterface<TInterfaceType>(object obj)
		{
			return (TInterfaceType)QueryInterface(obj, typeof(TInterfaceType).GUID);
		}
		
		public static object QueryInterface(object obj, Guid iid)
		{
			if (obj == null)
				return null;
			
			IntPtr pUnk = Marshal.GetIUnknownForObject(obj);
			if (pUnk == IntPtr.Zero)
				return null;
						
			IntPtr ppv;
			Marshal.QueryInterface(pUnk, ref iid, out ppv);
			
			object result = (ppv != IntPtr.Zero) ? Marshal.GetObjectForIUnknown(ppv) : null;
			
			Marshal.Release(pUnk);
			if (ppv != IntPtr.Zero)
				Marshal.Release(ppv);
			
			return result;
		}
		
		public static object GetService(Guid classIID)
		{
			Guid iid = typeof(nsISupports).GUID;
			return ServiceManager.GetService(ref classIID, ref iid);
		}
		
		public static object GetService(string contractID)
		{
			return GetService<nsISupports>(contractID);
		}
		
		public static TInterfaceType GetService<TInterfaceType>(string contractID)
		{
			Guid iid = typeof(TInterfaceType).GUID;
			return (TInterfaceType)ServiceManager.GetServiceByContractID(contractID, ref iid);
		}
		
		/// <summary>
		/// Registers a factory to be used to instantiate a particular class identified by ClassID, and creates an association of class name and ContractID with the class.
		/// </summary>
		/// <param name="classID">The ClassID of the class being registered.</param>
		/// <param name="className">The name of the class being registered. This value is intended as a human-readable name for the class and need not be globally unique.</param>
		/// <param name="contractID">The ContractID of the class being registered.</param>
		/// <param name="factory">The nsIFactory instance of the class being registered.</param>
		internal static void RegisterFactory(Guid classID, string className, string contractID, nsIFactory factory)
		{
			ComponentRegistrar.RegisterFactory(ref classID, className, contractID, factory);
		}
	}
}
