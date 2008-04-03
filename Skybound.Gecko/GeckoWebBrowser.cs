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
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.IO;
using System.Reflection;
using System.Diagnostics;

namespace Skybound.Gecko
{
	/// <summary>
	/// A Gecko-based web browser.
	/// </summary>
	public partial class GeckoWebBrowser : Control,
		nsIWebBrowserChrome,
		nsIContextMenuListener2,
		nsIWebProgressListener,
		nsIInterfaceRequestor,
		nsIEmbeddingSiteWindow2,
		nsISupportsWeakReference,
		nsIWeakReference,
		nsIDOMEventListener,
		nsISHistoryListener
	{
		/// <summary>
		/// Initializes a new instance of <see cref="GeckoWebBrowser"/>.
		/// </summary>
		public GeckoWebBrowser()
		{
		}
		
		//static Dictionary<nsIDOMDocument, GeckoWebBrowser> FromDOMDocumentTable = new Dictionary<nsIDOMDocument,GeckoWebBrowser>();
		
		//internal static GeckoWebBrowser FromDOMDocument(nsIDOMDocument doc)
		//{
		//      GeckoWebBrowser result;
		//      return FromDOMDocumentTable.TryGetValue(doc, out result) ? result : null;
		//}
		
		#region protected override void Dispose(bool disposing)
		protected override void Dispose(bool disposing)
		{
			// make sure the object is still alove before we call a method on it
			if (Xpcom.QueryInterface<nsIWebNavigation>(WebNav) != null)
			{
				WebNav.Stop(nsIWebNavigationConstants.STOP_ALL);
			}
			WebNav = null;
			
			if (Xpcom.QueryInterface<nsIBaseWindow>(BaseWindow) != null)
			{
				BaseWindow.Destroy();
			}
			BaseWindow = null;
			
			base.Dispose(disposing);
		}
		#endregion
		
		nsIWebBrowser WebBrowser;
		nsIWebBrowserFocus WebBrowserFocus;
		nsIBaseWindow BaseWindow;
		nsIWebNavigation WebNav;
		int ChromeFlags;
		
		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			
			if (!this.DesignMode)
			{
				Xpcom.Initialize();
				PromptServiceFactory.Register();
				//WindowCreator.Register();
				
				WebBrowser = Xpcom.CreateInstance<nsIWebBrowser>("@mozilla.org/embedding/browser/nsWebBrowser;1");
				WebBrowserFocus = (nsIWebBrowserFocus)WebBrowser;
				BaseWindow = (nsIBaseWindow)WebBrowser;
				WebNav = (nsIWebNavigation)WebBrowser;
				
				WebBrowser.SetContainerWindow(this);
				
				((nsIDocShellTreeItem)WebBrowser).SetItemType(nsIDocShellTreeItemConstants.typeContentWrapper);
				
				BaseWindow.InitWindow(this.Handle, IntPtr.Zero, 0, 0, this.Width, this.Height);
				BaseWindow.Create();
				
				Guid nsIWebProgressListenerGUID = typeof(nsIWebProgressListener).GUID;
				WebBrowser.AddWebBrowserListener(this, ref nsIWebProgressListenerGUID);
				
				nsIDOMEventTarget target = ((nsIDOMWindow2)WebBrowser.GetContentDOMWindow()).GetWindowRoot();
				
				target.AddEventListener(new nsAString("submit"), this, true);
				target.AddEventListener(new nsAString("keydown"), this, true);
				target.AddEventListener(new nsAString("keyup"), this, true);
				target.AddEventListener(new nsAString("mousemove"), this, true);
				target.AddEventListener(new nsAString("mouseover"), this, true);
				target.AddEventListener(new nsAString("mouseout"), this, true);
				target.AddEventListener(new nsAString("mousedown"), this, true);
				target.AddEventListener(new nsAString("mouseup"), this, true);
				
				// history
				WebNav.GetSessionHistory().AddSHistoryListener(this);
				
				BaseWindow.SetVisibility(true);
				
				// navigating to about:blank allows drag & drop to work properly before a page has been loaded into the browser
				Navigate("about:blank");
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			if (this.DesignMode)
			{
				string versionString = ((AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(GetType().Assembly, typeof(AssemblyFileVersionAttribute))).Version;
				string copyright = ((AssemblyCopyrightAttribute)Attribute.GetCustomAttribute(GetType().Assembly, typeof(AssemblyCopyrightAttribute))).Copyright;
				
				using (Brush brush = new System.Drawing.Drawing2D.HatchBrush(System.Drawing.Drawing2D.HatchStyle.SolidDiamond, Color.FromArgb(240, 240, 240), Color.White))
					e.Graphics.FillRectangle(brush, this.ClientRectangle);
				
				e.Graphics.DrawString("Skybound GeckoFX v" + versionString + "\r\n" + copyright + "\r\n" + "http://www.geckofx.org", SystemFonts.MessageBoxFont, Brushes.Black,
					new RectangleF(2, 2, this.Width-4, this.Height-4));
				e.Graphics.DrawRectangle(SystemPens.ControlDark, 0, 0, Width-1, Height-1);
			}
			base.OnPaint(e);
		}
		
		class WindowCreator : nsIWindowCreator
		{
			static WindowCreator()
			{
				// give an nsIWindowCreator to the WindowWatcher service
				nsIWindowWatcher watcher = Xpcom.GetService<nsIWindowWatcher>("@mozilla.org/embedcomp/window-watcher;1");
				if (watcher != null)
				{
					//disabled for now because it's not loading the proper URL automatically
					watcher.SetWindowCreator(new WindowCreator());
				}
			}
			
			public static void Register()
			{
				// calling this method simply invokes the static ctor
			}
			
			public nsIWebBrowserChrome CreateChromeWindow(nsIWebBrowserChrome parent, uint chromeFlags)
			{
				GeckoWebBrowser browser = parent as GeckoWebBrowser;
				if (browser != null)
				{
					GeckoCreateWindowEventArgs e = new GeckoCreateWindowEventArgs((GeckoWindowFlags)chromeFlags);
					browser.OnCreateWindow(e);
					return e.WebBrowser;
				}
				return null;
			}
		}
		
		#region public event GeckoCreateWindowEventHandler CreateWindow
		public event GeckoCreateWindowEventHandler CreateWindow
		{
			add { this.Events.AddHandler(CreateWindowEvent, value); }
			remove { this.Events.RemoveHandler(CreateWindowEvent, value); }
		}
		private static object CreateWindowEvent = new object();

		/// <summary>Raises the <see cref="CreateWindow"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnCreateWindow(GeckoCreateWindowEventArgs e)
		{
			if (((GeckoCreateWindowEventHandler)this.Events[CreateWindowEvent]) != null)
				((GeckoCreateWindowEventHandler)this.Events[CreateWindowEvent])(this, e);
		}
		#endregion
		
		#region public event GeckoWindowSetSizeEventHandler WindowSetSize
		public event GeckoWindowSetSizeEventHandler WindowSetSize
		{
			add { this.Events.AddHandler(WindowSetSizeEvent, value); }
			remove { this.Events.RemoveHandler(WindowSetSizeEvent, value); }
		}
		private static object WindowSetSizeEvent = new object();

		/// <summary>Raises the <see cref="WindowSetSize"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnWindowSetSize(GeckoWindowSetSizeEventArgs e)
		{
			if (((GeckoWindowSetSizeEventHandler)this.Events[WindowSetSizeEvent]) != null)
				((GeckoWindowSetSizeEventHandler)this.Events[WindowSetSizeEvent])(this, e);
		}
		#endregion
		
		#region protected override void WndProc(ref Message m)
		[DllImport("user32")]
		private static extern bool IsChild(IntPtr hWndParent, IntPtr hwnd);
		
		[DllImport("user32")]
		private static extern IntPtr GetFocus();

		protected override void WndProc(ref Message m)
		{
			const int WM_GETDLGCODE = 0x87;
			const int DLGC_WANTALLKEYS = 0x4;
			const int WM_MOUSEACTIVATE = 0x21;
			const int MA_ACTIVATE = 0x1;
			
			if (!DesignMode)
			{
				if (m.Msg == WM_GETDLGCODE)
				{
					m.Result = (IntPtr)DLGC_WANTALLKEYS;
					return;
				}
				else if (m.Msg == WM_MOUSEACTIVATE)
				{
					m.Result = (IntPtr)MA_ACTIVATE;
					
					if (!IsChild(Handle, GetFocus()))
					{
						WebBrowserFocus.Activate();
					}
					return;
				}
			}
			
			base.WndProc(ref m);
		}
		#endregion
		
		#region Overridden Properties & Event Handlers Handlers
		[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public override Color BackColor
		{
			get { return base.BackColor; }
			set { base.BackColor = value; }
		}
		
		[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public override Image BackgroundImage
		{
			get { return base.BackgroundImage; }
			set { base.BackgroundImage = value; }
		}
		
		[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public override ImageLayout BackgroundImageLayout
		{
			get { return base.BackgroundImageLayout; }
			set { base.BackgroundImageLayout = value; }
		}
		
		[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public override Color ForeColor
		{
			get { return base.ForeColor; }
			set { base.ForeColor = value; }
		}
		
		[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public override Font Font
		{
			get { return base.Font; }
			set { base.Font = value; }
		}
		
		[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public override string Text
		{
			get { return base.Text; }
			set { base.Text = value; }
		}
		
		protected override void OnEnter(EventArgs e)
		{
			if (!IsBusy)
				WebBrowserFocus.Activate();
			
			base.OnEnter(e);
		}

		protected override void OnLeave(EventArgs e)
		{
			if (!IsBusy)
				WebBrowserFocus.Deactivate();
			
			base.OnLeave(e);
		}

		protected override void OnSizeChanged(EventArgs e)
		{
			if (BaseWindow != null)
			{
				BaseWindow.SetPositionAndSize(0, 0, ClientSize.Width, ClientSize.Height, true);
			}
			
			base.OnSizeChanged(e);
		}
		#endregion
		
		/// <summary>
		/// Navigates to the specified URL.
		/// </summary>
		/// <param name="url">The url to navigate to.</param>
		public void Navigate(string url)
		{
			Navigate(url, 0);
		}
		
		/// <summary>
		/// Navigates to the specified URL using the given load flags.
		/// </summary>
		/// <param name="url">The url to navigate to.</param>
		/// <param name="loadFlags">Flags which specify how the page is loaded.</param>
		public bool Navigate(string url, GeckoLoadFlags loadFlags)
		{
			if (url == null)
				throw new ArgumentNullException("url");
			
			if (IsHandleCreated)
			{
				if (IsBusy)
					this.Stop();
				
				// WebNav.LoadURI throws an exception if we try to open a file that doesn't exist...
				Uri created;
				if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out created) && created.IsAbsoluteUri && created.IsFile)
				{
					if (!File.Exists(created.LocalPath) && !Directory.Exists(created.LocalPath))
						return false;
				}
				
				if (WebNav.LoadURI(url, (uint)loadFlags, null, IntPtr.Zero, IntPtr.Zero) != 0)
					return false;
					// failed
			}
			else
			{
				throw new InvalidOperationException("Cannot call Navigate() before the window handle is created.");
			}
			
			return true;
		}
		
		/// <summary>
		/// Gets or sets the text displayed in the status bar.
		/// </summary>
		[Browsable(false), DefaultValue("")]
		public string StatusText
		{
			get { return _StatusText ?? ""; }
			set
			{
				if (StatusText != value)
				{
					_StatusText = value;
					OnStatusTextChanged(EventArgs.Empty);
				}
			}
		}
		string _StatusText;

		#region public event EventHandler StatusTextChanged
		/// <summary>
		/// Occurs when the value of the <see cref="StatusText"/> property is changed.
		/// </summary>
		[Category("Property Changed"), Description("Occurs when the value of the StatusText property is changed.")]
		public event EventHandler StatusTextChanged
		{
			add { this.Events.AddHandler(StatusTextChangedEvent, value); }
			remove { this.Events.RemoveHandler(StatusTextChangedEvent, value); }
		}
		private static object StatusTextChangedEvent = new object();

		/// <summary>Raises the <see cref="StatusTextChanged"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnStatusTextChanged(EventArgs e)
		{
			if (((EventHandler)this.Events[StatusTextChangedEvent]) != null)
				((EventHandler)this.Events[StatusTextChangedEvent])(this, e);
		}
		#endregion
		
		/// <summary>
		/// Gets the title of the document loaded into the web browser.
		/// </summary>
		[Browsable(false), DefaultValue("")]
		public string DocumentTitle
		{
			get { return _DocumentTitle ?? ""; }
			private set
			{
				if (DocumentTitle != value)
				{
					_DocumentTitle = value;
					OnDocumentTitleChanged(EventArgs.Empty);
				}
			}
		}
		string _DocumentTitle;
		
		#region public event EventHandler DocumentTitleChanged
		/// <summary>
		/// Occurs when the value of the <see cref="DocumentTitle"/> property is changed.
		/// </summary>
		[Category("Property Changed"), Description("Occurs when the value of the DocumentTitle property is changed.")]
		public event EventHandler DocumentTitleChanged
		{
			add { this.Events.AddHandler(DocumentTitleChangedEvent, value); }
			remove { this.Events.RemoveHandler(DocumentTitleChangedEvent, value); }
		}
		private static object DocumentTitleChangedEvent = new object();

		/// <summary>Raises the <see cref="DocumentTitleChanged"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnDocumentTitleChanged(EventArgs e)
		{
			if (((EventHandler)this.Events[DocumentTitleChangedEvent]) != null)
				((EventHandler)this.Events[DocumentTitleChangedEvent])(this, e);
		}
		#endregion
		
		/// <summary>
		/// Gets whether the browser may navigate back in the history.
		/// </summary>
		[BrowsableAttribute(false)]
		public bool CanGoBack
		{
			get { return _CanGoBack; }
		}
		bool _CanGoBack;

		#region public event EventHandler CanGoBackChanged
		/// <summary>
		/// Occurs when the value of the <see cref="CanGoBack"/> property is changed.
		/// </summary>
		[Category("Property Changed"), Description("Occurs when the value of the CanGoBack property is changed.")]
		public event EventHandler CanGoBackChanged
		{
			add { this.Events.AddHandler(CanGoBackChangedEvent, value); }
			remove { this.Events.RemoveHandler(CanGoBackChangedEvent, value); }
		}
		private static object CanGoBackChangedEvent = new object();

		/// <summary>Raises the <see cref="CanGoBackChanged"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnCanGoBackChanged(EventArgs e)
		{
			if (((EventHandler)this.Events[CanGoBackChangedEvent]) != null)
				((EventHandler)this.Events[CanGoBackChangedEvent])(this, e);
		}
		#endregion
		
		/// <summary>
		/// Gets whether the browser may navigate forward in the history.
		/// </summary>
		[BrowsableAttribute(false)]
		public bool CanGoForward
		{
			get { return _CanGoForward; }
		}
		bool _CanGoForward;

		#region public event EventHandler CanGoForwardChanged
		/// <summary>
		/// Occurs when the value of the <see cref="CanGoForward"/> property is changed.
		/// </summary>
		[Category("Property Changed"), Description("Occurs when the value of the CanGoForward property is changed.")]
		public event EventHandler CanGoForwardChanged
		{
			add { this.Events.AddHandler(CanGoForwardChangedEvent, value); }
			remove { this.Events.RemoveHandler(CanGoForwardChangedEvent, value); }
		}
		private static object CanGoForwardChangedEvent = new object();

		/// <summary>Raises the <see cref="CanGoForwardChanged"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnCanGoForwardChanged(EventArgs e)
		{
			if (((EventHandler)this.Events[CanGoForwardChangedEvent]) != null)
				((EventHandler)this.Events[CanGoForwardChangedEvent])(this, e);
		}
		#endregion
		
		/// <summary>Raises the CanGoBackChanged or CanGoForwardChanged events when necessary.</summary>
		void UpdateCommandStatus()
		{
			bool canGoBack = false;
			bool canGoForward = false;
			if (WebNav != null)
			{
				canGoBack = WebNav.GetCanGoBack();
				canGoForward = WebNav.GetCanGoForward();
			}
			
			if (_CanGoBack != canGoBack)
			{
				_CanGoBack = canGoBack;
				OnCanGoBackChanged(EventArgs.Empty);
			}
			
			if (_CanGoForward != canGoForward)
			{
				_CanGoForward = canGoForward;
				OnCanGoForwardChanged(EventArgs.Empty);
			}
		}
		
		/// <summary>
		/// Navigates to the previous page in the history, if one is available.
		/// </summary>
		/// <returns></returns>
		public bool GoBack()
		{
			if (CanGoBack)
			{
				WebNav.GoBack();
				return true;
			}
			return false;
		}
		
		/// <summary>
		/// Navigates to the next page in the history, if one is available.
		/// </summary>
		/// <returns></returns>
		public bool GoForward()
		{
			if (CanGoForward)
			{
				WebNav.GoForward();
				return true;
			}
			return false;
		}
		
		/// <summary>
		/// Cancels any pending navigation and also stops any sound or animation.
		/// </summary>
		public void Stop()
		{
			if (WebNav != null)
				WebNav.Stop(nsIWebNavigationConstants.STOP_ALL);
		}
		
		/// <summary>
		/// Reloads the current page.
		/// </summary>
		/// <returns></returns>
		public bool Reload()
		{
		    return Reload(GeckoLoadFlags.None);
		}
		
		/// <summary>
		/// Reloads the current page using the specified flags.
		/// </summary>
		/// <param name="flags"></param>
		/// <returns></returns>
		public bool Reload(GeckoLoadFlags flags)
		{
			if (WebNav != null)
				WebNav.Reload((uint)flags); 
			return true;
		}
		
		/// <summary>
		/// Gets the <see cref="Url"/> currently displayed in the web browser.
		/// Use the <see cref="Navigate(string)"/> method to change the URL.
		/// </summary>
		[BrowsableAttribute(false)]
		public Uri Url
		{
			get
			{
				if (WebNav == null)
					return null;
				
				nsURI location = WebNav.GetCurrentURI();
				if (!location.IsNull)
				{
					return new Uri(location.Spec);
				}
				
				return new Uri("about:blank");
			}
		}
		
		/// <summary>
		/// Gets the <see cref="Url"/> of the current page's referrer.
		/// </summary>
		[BrowsableAttribute(false)]
		public Uri ReferrerUrl
		{
			get
			{
				if (WebNav == null)
					return null;
				
				nsIURI location = WebNav.GetReferringURI();
				if (location != null)
				{
					return new Uri(nsString.Get(location.GetSpec));
				}
				
				return new Uri("about:blank");
			}
		}
		
		/// <summary>
		/// Gets the <see cref="GeckoWindow"/> object for this browser.
		/// </summary>
		[Browsable(false)]
		public GeckoWindow Window
		{
			get
			{
				if (WebBrowser == null)
					return null;
				
				if (_Window == null)
				{
					_Window = GeckoWindow.Create((nsIDOMWindow2)WebBrowser.GetContentDOMWindow());
				}
				return _Window;
			}
		}
		GeckoWindow _Window;
		
		/// <summary>
		/// Gets the <see cref="GeckoDocument"/> for the page currently loaded in the browser.
		/// </summary>
		[Browsable(false)]
		public GeckoDocument Document
		{
			get
			{
				if (WebBrowser == null)
					return null;
				
				if (_Document == null)
				{
					_Document = GeckoDocument.Create((nsIDOMHTMLDocument)WebBrowser.GetContentDOMWindow().GetDocument());
					//FromDOMDocumentTable.Add((nsIDOMDocument)_Document.DomObject, this);
				}
				return _Document;
			}
		}
		GeckoDocument _Document;
		
		private void UnloadDocument()
		{
			//if (_Document != null)
			//{
			//      FromDOMDocumentTable.Remove((nsIDOMDocument)_Document.DomObject);
			//}
			_Document = null;
		}
		
		#region public event GeckoNavigatingEventHandler Navigating
		/// <summary>
		/// Occurs before the browser navigates to a new page.
		/// </summary>
		[Category("Navigation"), Description("Occurs before the browser navigates to a new page.")]
		public event GeckoNavigatingEventHandler Navigating
		{
			add { this.Events.AddHandler(NavigatingEvent, value); }
			remove { this.Events.RemoveHandler(NavigatingEvent, value); }
		}
		private static object NavigatingEvent = new object();

		/// <summary>Raises the <see cref="Navigating"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnNavigating(GeckoNavigatingEventArgs e)
		{
			if (((GeckoNavigatingEventHandler)this.Events[NavigatingEvent]) != null)
				((GeckoNavigatingEventHandler)this.Events[NavigatingEvent])(this, e);
		}
		#endregion
		
		#region public event GeckoNavigatedEventHandler Navigated
		/// <summary>
		/// Occurs after the browser has navigated to a new page.
		/// </summary>
		[Category("Navigation"), Description("Occurs after the browser has navigated to a new page.")]
		public event GeckoNavigatedEventHandler Navigated
		{
			add { this.Events.AddHandler(NavigatedEvent, value); }
			remove { this.Events.RemoveHandler(NavigatedEvent, value); }
		}
		private static object NavigatedEvent = new object();

		/// <summary>Raises the <see cref="Navigated"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnNavigated(GeckoNavigatedEventArgs e)
		{
			if (((GeckoNavigatedEventHandler)this.Events[NavigatedEvent]) != null)
				((GeckoNavigatedEventHandler)this.Events[NavigatedEvent])(this, e);
		}
		#endregion
		
		#region public event EventHandler DocumentCompleted
		/// <summary>
		/// Occurs after the browser has finished parsing a new page and updated the <see cref="Document"/> property.
		/// </summary>
		[Category("Navigation"), Description("Occurs after the browser has finished parsing a new page and updated the Document property.")]
		public event EventHandler DocumentCompleted
		{
			add { this.Events.AddHandler(DocumentCompletedEvent, value); }
			remove { this.Events.RemoveHandler(DocumentCompletedEvent, value); }
		}
		private static object DocumentCompletedEvent = new object();

		/// <summary>Raises the <see cref="DocumentCompleted"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnDocumentCompleted(EventArgs e)
		{
			if (((EventHandler)this.Events[DocumentCompletedEvent]) != null)
				((EventHandler)this.Events[DocumentCompletedEvent])(this, e);
		}
		#endregion
		
		/// <summary>
		/// Gets whether the browser is busy loading a page.
		/// </summary>
		[Browsable(false)]
		public bool IsBusy
		{
			get { return _IsBusy; }
			private set { _IsBusy = value; }
		}
		bool _IsBusy;
		
		#region public event GeckoProgressEventHandler ProgressChanged
		/// <summary>
		/// Occurs when the control has updated progress information.
		/// </summary>
		[Category("Navigation"), Description("Occurs when the control has updated progress information.")]
		public event GeckoProgressEventHandler ProgressChanged
		{
			add { this.Events.AddHandler(ProgressChangedEvent, value); }
			remove { this.Events.RemoveHandler(ProgressChangedEvent, value); }
		}
		private static object ProgressChangedEvent = new object();

		/// <summary>Raises the <see cref="ProgressChanged"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnProgressChanged(GeckoProgressEventArgs e)
		{
			if (((GeckoProgressEventHandler)this.Events[ProgressChangedEvent]) != null)
				((GeckoProgressEventHandler)this.Events[ProgressChangedEvent])(this, e);
		}
		#endregion
		
		/// <summary>
		/// Saves the current document to the specified file name.
		/// </summary>
		/// <param name="filename"></param>
		/// <returns></returns>
		public void SaveDocument(string filename)
		{
			if (!Directory.Exists(Path.GetDirectoryName(filename)))
				throw new System.IO.DirectoryNotFoundException();
			else if (this.Document == null)
				throw new InvalidOperationException("No document has been loaded into the web browser.");
			
			nsIWebBrowserPersist persist = Xpcom.QueryInterface<nsIWebBrowserPersist>(WebBrowser);
			if (persist != null)
			{
				persist.SaveDocument((nsIDOMDocument)Document.DomObject, Xpcom.NewNativeLocalFile(filename), null,
					null, 0, 0);
			}
			else
			{
				throw new InvalidOperationException();
			}
		}
		
		/// <summary>
		/// Gets the session history for the current browser.
		/// </summary>
		[Browsable(false)]
		public GeckoSessionHistory History
		{
			get
			{
				if (WebNav == null)
					return null;
				
				return (_History == null) ? (_History = new GeckoSessionHistory(WebNav)) : _History;
			}
		}
		GeckoSessionHistory _History;
		
		#region nsIWebBrowserChrome Members

		void nsIWebBrowserChrome.SetStatus(int statusType, string status)
		{
			this.StatusText = status;
		}

		nsIWebBrowser nsIWebBrowserChrome.GetWebBrowser()
		{
			return this.WebBrowser;
		}

		void nsIWebBrowserChrome.SetWebBrowser(nsIWebBrowser webBrowser)
		{
			this.WebBrowser = webBrowser;
		}

		int nsIWebBrowserChrome.GetChromeFlags()
		{
			return this.ChromeFlags;
		}

		void nsIWebBrowserChrome.SetChromeFlags(int flags)
		{
			this.ChromeFlags = flags;
		}
		
		void nsIWebBrowserChrome.DestroyBrowserWindow()
		{
			//throw new NotImplementedException();
			OnWindowClosed(EventArgs.Empty);
		}
		
		#region public event EventHandler WindowClosed
		public event EventHandler WindowClosed
		{
			add { this.Events.AddHandler(WindowClosedEvent, value); }
			remove { this.Events.RemoveHandler(WindowClosedEvent, value); }
		}
		private static object WindowClosedEvent = new object();

		/// <summary>Raises the <see cref="WindowClosed"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnWindowClosed(EventArgs e)
		{
			if (((EventHandler)this.Events[WindowClosedEvent]) != null)
				((EventHandler)this.Events[WindowClosedEvent])(this, e);
		}
		#endregion
		
		void nsIWebBrowserChrome.SizeBrowserTo(int cx, int cy)
		{
			OnWindowSetSize(new GeckoWindowSetSizeEventArgs(new Size(cx, cy)));
		}

		void nsIWebBrowserChrome.ShowAsModal()
		{
			//throw new NotImplementedException();
			Debug.WriteLine("ShowAsModal");
			_IsWindowModal = true;
		}
		bool _IsWindowModal;

		bool nsIWebBrowserChrome.IsWindowModal()
		{
			//throw new NotImplementedException();
			Debug.WriteLine("IsWindowModal");
			return _IsWindowModal;
		}

		void nsIWebBrowserChrome.ExitModalEventLoop(int status)
		{
			//throw new NotImplementedException();
			Debug.WriteLine("ExitModalEventLoop");
			_IsWindowModal = false;
		}

		#endregion

		#region nsIContextMenuListener2 Members

		void nsIContextMenuListener2.OnShowContextMenu(uint aContextFlags, nsIContextMenuInfo info)
		{
			MenuItem mnuBack = new MenuItem("Back");
			mnuBack.Click += delegate { GoBack(); };
			mnuBack.Enabled = this.CanGoBack;
			
			MenuItem mnuForward = new MenuItem("Forward");
			mnuForward.Click += delegate { GoForward(); };
			mnuForward.Enabled = this.CanGoForward;
			
			MenuItem mnuSelectAll = new MenuItem("Select All");
			mnuSelectAll.Click += delegate { SelectAll(); };
			
			GeckoDocument doc = GeckoDocument.Create((nsIDOMHTMLDocument)info.GetTargetNode().GetOwnerDocument());
			
			string viewSourceUrl = (doc == null) ? null : Convert.ToString(doc.Url);
			
			MenuItem mnuViewSource = new MenuItem("View Source");
			mnuViewSource.Enabled = !string.IsNullOrEmpty(viewSourceUrl);
			mnuViewSource.Click += delegate { ViewSource(viewSourceUrl); };
			
			string properties = (doc.Url == Document.Url) ? "Page Properties" : "IFRAME Properties";
			
			MenuItem mnuProperties = new MenuItem(properties);
			mnuProperties.Enabled = true;
			mnuProperties.Click += delegate { ShowPageProperties(doc); };
			
			ContextMenu menu = new ContextMenu();
			menu.MenuItems.Add(mnuBack);
			menu.MenuItems.Add(mnuForward);
			menu.MenuItems.Add("-");
			menu.MenuItems.Add(mnuSelectAll);
			menu.MenuItems.Add("-");
			menu.MenuItems.Add(mnuViewSource);
			menu.MenuItems.Add(mnuProperties);
			
			menu.Show(this, PointToClient(MousePosition));
		}
		
		/// <summary>
		/// Selects the entire document.
		/// </summary>
		public void SelectAll()
		{
			if (Window == null || Document == null)
				throw new InvalidOperationException();
			
			if (Document.Body == null)
				return;
			
			((nsIDOMWindow2)Window.DomWindow).GetSelection().SelectAllChildren(
				(nsIDOMNode)Document.Body.DomObject);
		}
		
		/// <summary>
		/// Opens a new window which contains the source code for the current page.
		/// </summary>
		public void ViewSource()
		{
			ViewSource(Url.ToString());
		}
		
		/// <summary>
		/// Opens a new window which contains the source code for the specified page.
		/// </summary>
		/// <param name="url"></param>
		public void ViewSource(string url)
		{
			Form form = new Form();
			form.Text = "View Source";
			GeckoWebBrowser browser = new GeckoWebBrowser();
			browser.Dock = DockStyle.Fill;
			form.Controls.Add(browser);
			form.Load += delegate { browser.Navigate("view-source:" + url); };
			form.Icon = FindForm().Icon;
			form.ClientSize = this.ClientSize;
			form.StartPosition = FormStartPosition.CenterParent;
			
			form.Show();
		}
		
		/// <summary>
		/// Displays a properties dialog for the current page.
		/// </summary>
		public void ShowPageProperties()
		{
			ShowPageProperties(Document);
		}
		
		public void ShowPageProperties(GeckoDocument document)
		{
			if (document == null)
				throw new ArgumentNullException("document");
			
			new PropertiesDialog((nsIDOMHTMLDocument)document.DomObject).ShowDialog(this);
		}
		
		#endregion

		#region nsIInterfaceRequestor Members

		IntPtr nsIInterfaceRequestor.GetInterface(ref Guid uuid)
		{
			IntPtr ppv, pUnk = Marshal.GetIUnknownForObject(this);
			
			Marshal.QueryInterface(pUnk, ref uuid, out ppv);
			
			Marshal.Release(pUnk);
			
			return ppv;
		}

		#endregion

		#region nsIEmbeddingSiteWindow2 Members

		void nsIEmbeddingSiteWindow.SetDimensions(uint flags, int x, int y, int cx, int cy)
		{
			throw new NotImplementedException();
		}

		void nsIEmbeddingSiteWindow.GetDimensions(uint flags, ref int x, ref int y, ref int cx, ref int cy)
		{
			if ((flags & nsIEmbeddingSiteWindowConstants.DIM_FLAGS_POSITION) != 0)
			{
				x = Left;
				y = Top;
			}

			if ((flags & nsIEmbeddingSiteWindowConstants.DIM_FLAGS_SIZE_INNER) != 0)
			{
				cx = ClientSize.Width;
				cy = ClientSize.Height;
			}
			else
			{
				cx = Width;
				cy = Height;
			}
		}

		void nsIEmbeddingSiteWindow.SetFocus()
		{
			Focus();
			WebBrowserFocus.Activate();
			BaseWindow.SetFocus();
		}

		bool nsIEmbeddingSiteWindow.GetVisibility()
		{
			return Visible;
		}

		void nsIEmbeddingSiteWindow.SetVisibility(bool aVisibility)
		{
			Visible = aVisibility;
		}

		string nsIEmbeddingSiteWindow.GetTitle()
		{
			return DocumentTitle;
		}

		void nsIEmbeddingSiteWindow.SetTitle(string aTitle)
		{
			DocumentTitle = aTitle;
		}

		IntPtr nsIEmbeddingSiteWindow.GetSiteWindow()
		{
			return Handle;
		}

		#endregion

		#region nsIEmbeddingSiteWindow2 Members
		
		void nsIEmbeddingSiteWindow2.SetDimensions(uint flags, int x, int y, int cx, int cy)
		{
			(this as nsIEmbeddingSiteWindow).SetDimensions(flags, x, y, cx, y);
		}

		void nsIEmbeddingSiteWindow2.GetDimensions(uint flags, ref int x, ref int y, ref int cx, ref int cy)
		{
			(this as nsIEmbeddingSiteWindow).GetDimensions(flags, ref x, ref y, ref cx, ref y);
		}

		void nsIEmbeddingSiteWindow2.SetFocus()
		{
			(this as nsIEmbeddingSiteWindow).SetFocus();
		}

		bool nsIEmbeddingSiteWindow2.GetVisibility()
		{
			return (this as nsIEmbeddingSiteWindow).GetVisibility();
		}

		void nsIEmbeddingSiteWindow2.SetVisibility(bool aVisibility)
		{
			(this as nsIEmbeddingSiteWindow).SetVisibility(aVisibility);
		}

		string nsIEmbeddingSiteWindow2.GetTitle()
		{
			return (this as nsIEmbeddingSiteWindow).GetTitle();
		}

		void nsIEmbeddingSiteWindow2.SetTitle(string aTitle)
		{
			(this as nsIEmbeddingSiteWindow).SetTitle(aTitle);
		}

		IntPtr nsIEmbeddingSiteWindow2.GetSiteWindow()
		{
			return (this as nsIEmbeddingSiteWindow).GetSiteWindow();
		}
		
		void nsIEmbeddingSiteWindow2.Blur()
		{
		      //throw new NotImplementedException();
		}

		#endregion
		
		#region nsISupportsWeakReference Members

		nsIWeakReference nsISupportsWeakReference.GetWeakReference()
		{
			return this;
		}

		#endregion

		#region nsIWeakReference Members

		IntPtr nsIWeakReference.QueryReferent(ref Guid uuid)
		{
			IntPtr ppv, pUnk = Marshal.GetIUnknownForObject(this);
			
			Marshal.QueryInterface(pUnk, ref uuid, out ppv);
			
			Marshal.Release(pUnk);
			
			if (ppv != IntPtr.Zero)
			{
				Marshal.Release(ppv);
			}
			
			return ppv;
		}

		#endregion
		
		#region nsIWebProgressListener Members

		void nsIWebProgressListener.OnStateChange(nsIWebProgress aWebProgress, nsIRequest aRequest, int aStateFlags, int aStatus)
		{
			if ((aStateFlags & nsIWebProgressListenerConstants.STATE_START) != 0 && (aStateFlags & nsIWebProgressListenerConstants.STATE_IS_NETWORK) != 0)
			{
				IsBusy = true;
				
				GeckoNavigatingEventArgs ea = new GeckoNavigatingEventArgs(null);
				OnNavigating(ea);
				
				if (ea.Cancel)
					aRequest.Cancel(0);
			}
			
			if ((aStateFlags & nsIWebProgressListenerConstants.STATE_STOP) != 0 && (aStateFlags & nsIWebProgressListenerConstants.STATE_IS_NETWORK) != 0)
			{
				// clear busy state
				IsBusy = false;
				
				// kill any cached document and raise DocumentCompleted event
				UnloadDocument();
				OnDocumentCompleted(EventArgs.Empty);
				
				// clear progress bar
				OnProgressChanged(new GeckoProgressEventArgs(100, 100));
				
				// clear status bar
				StatusText = "";
			}
		}

		void nsIWebProgressListener.OnProgressChange(nsIWebProgress aWebProgress, nsIRequest aRequest, int aCurSelfProgress, int aMaxSelfProgress, int aCurTotalProgress, int aMaxTotalProgress)
		{
			int nProgress = aCurTotalProgress;
			int nProgressMax = Math.Max(aMaxTotalProgress, 0);
			
			if (nProgressMax == 0)
				nProgressMax = Int32.MaxValue;
			
			if (nProgress > nProgressMax)
				nProgress = nProgressMax;
			
			OnProgressChanged(new GeckoProgressEventArgs(nProgress, nProgressMax));
		}

		void nsIWebProgressListener.OnLocationChange(nsIWebProgress aWebProgress, nsIRequest aRequest, nsIURI aLocation)
		{
			// make sure we're loading the top-level window
			nsIDOMWindow domWindow = aWebProgress.GetDOMWindow();
			if (domWindow != null)
			{
			      if (domWindow != domWindow.GetTop())
			            return;
			}
			
			Uri uri;
			using (nsACString str = new nsACString())
			{
				aLocation.GetSpec(str);
				uri = new Uri(str.ToString());
			}
			
			OnNavigated(new GeckoNavigatedEventArgs(uri));
			UpdateCommandStatus();
		}

		void nsIWebProgressListener.OnStatusChange(nsIWebProgress aWebProgress, nsIRequest aRequest, int aStatus, string aMessage)
		{
			if (aWebProgress.GetIsLoadingDocument())
			{
				StatusText = aMessage;
				UpdateCommandStatus();
			}
		}

		void nsIWebProgressListener.OnSecurityChange(nsIWebProgress aWebProgress, nsIRequest aRequest, int aState)
		{
			SetSecurityState((GeckoSecurityState) aState);
		}
		
		/// <summary>
		/// Gets a value which indicates whether the current page is secure.
		/// </summary>
		[Browsable(false)]
		public GeckoSecurityState SecurityState
		{
			get { return _SecurityState; }
		}
		GeckoSecurityState _SecurityState;
		
		void SetSecurityState(GeckoSecurityState value)
		{
			if (_SecurityState != value)
			{
				_SecurityState = value;
				OnSecurityStateChanged(EventArgs.Empty);
			}
		}
		
		#region public event EventHandler SecurityStateChanged
		/// <summary>
		/// Occurs when the value of the <see cref="SecurityState"/> property is changed.
		/// </summary>
		[Category("Property Changed"), Description("Occurs when the value of the SecurityState property is changed.")]
		public event EventHandler SecurityStateChanged
		{
			add { this.Events.AddHandler(SecurityStateChangedEvent, value); }
			remove { this.Events.RemoveHandler(SecurityStateChangedEvent, value); }
		}
		private static object SecurityStateChangedEvent = new object();

		/// <summary>Raises the <see cref="SecurityStateChanged"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnSecurityStateChanged(EventArgs e)
		{
			if (((EventHandler)this.Events[SecurityStateChangedEvent]) != null)
				((EventHandler)this.Events[SecurityStateChangedEvent])(this, e);
		}
		#endregion

		#endregion

		#region nsIDOMEventListener Members

		void nsIDOMEventListener.HandleEvent(nsIDOMEvent e)
		{
			string type;
			using (nsAString str = new nsAString())
			{
				e.GetType(str);
				type = str.ToString();
			}
			
			switch (type)
			{
				case "keydown": OnDomKeyDown(new GeckoDomKeyEventArgs((nsIDOMKeyEvent)e)); break;
				case "keyup": OnDomKeyUp(new GeckoDomKeyEventArgs((nsIDOMKeyEvent)e)); break;
				
				case "mousedown": OnDomMouseDown(new GeckoDomMouseEventArgs((nsIDOMMouseEvent)e)); break;
				case "mouseup": OnDomMouseUp(new GeckoDomMouseEventArgs((nsIDOMMouseEvent)e)); break;
				case "mousemove": OnDomMouseMove(new GeckoDomMouseEventArgs((nsIDOMMouseEvent)e)); break;
				case "mouseover": OnDomMouseOver(new GeckoDomMouseEventArgs((nsIDOMMouseEvent)e)); break;
				case "mouseout": OnDomMouseOut(new GeckoDomMouseEventArgs((nsIDOMMouseEvent)e)); break;
				
				case "submit":
					//TEMP: disable this because it's causing crashes
					//MessageBox.Show("Form submission has been disabled to improve stability in this version.", "Gecko", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
					
					//e.PreventDefault();
					//e.StopPropagation();
					break;
			}
		}

		#region public event GeckoDomKeyEventHandler DomKeyDown
		[Category("DOM Events")]
		public event GeckoDomKeyEventHandler DomKeyDown
		{
			add { this.Events.AddHandler(DomKeyDownEvent, value); }
			remove { this.Events.RemoveHandler(DomKeyDownEvent, value); }
		}
		private static object DomKeyDownEvent = new object();

		/// <summary>Raises the <see cref="DomKeyDown"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnDomKeyDown(GeckoDomKeyEventArgs e)
		{
			if (((GeckoDomKeyEventHandler)this.Events[DomKeyDownEvent]) != null)
				((GeckoDomKeyEventHandler)this.Events[DomKeyDownEvent])(this, e);
		}
		#endregion

		#region public event GeckoDomKeyEventHandler DomKeyUp
		[Category("DOM Events")]
		public event GeckoDomKeyEventHandler DomKeyUp
		{
			add { this.Events.AddHandler(DomKeyUpEvent, value); }
			remove { this.Events.RemoveHandler(DomKeyUpEvent, value); }
		}
		private static object DomKeyUpEvent = new object();

		/// <summary>Raises the <see cref="DomKeyUp"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnDomKeyUp(GeckoDomKeyEventArgs e)
		{
			if (((GeckoDomKeyEventHandler)this.Events[DomKeyUpEvent]) != null)
				((GeckoDomKeyEventHandler)this.Events[DomKeyUpEvent])(this, e);
		}
		#endregion
		
		#region public event GeckoDomMouseEventHandler DomMouseDown
		[Category("DOM Events")]
		public event GeckoDomMouseEventHandler DomMouseDown
		{
			add { this.Events.AddHandler(DomMouseDownEvent, value); }
			remove { this.Events.RemoveHandler(DomMouseDownEvent, value); }
		}
		private static object DomMouseDownEvent = new object();

		/// <summary>Raises the <see cref="DomMouseDown"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnDomMouseDown(GeckoDomMouseEventArgs e)
		{
			if (((GeckoDomMouseEventHandler)this.Events[DomMouseDownEvent]) != null)
				((GeckoDomMouseEventHandler)this.Events[DomMouseDownEvent])(this, e);
		}
		#endregion

		#region public event GeckoDomMouseEventHandler DomMouseUp
		[Category("DOM Events")]
		public event GeckoDomMouseEventHandler DomMouseUp
		{
			add { this.Events.AddHandler(DomMouseUpEvent, value); }
			remove { this.Events.RemoveHandler(DomMouseUpEvent, value); }
		}
		private static object DomMouseUpEvent = new object();

		/// <summary>Raises the <see cref="DomMouseUp"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnDomMouseUp(GeckoDomMouseEventArgs e)
		{
			if (((GeckoDomMouseEventHandler)this.Events[DomMouseUpEvent]) != null)
				((GeckoDomMouseEventHandler)this.Events[DomMouseUpEvent])(this, e);
		}
		#endregion

		#region public event GeckoDomMouseEventHandler DomMouseOver
		[Category("DOM Events")]
		public event GeckoDomMouseEventHandler DomMouseOver
		{
			add { this.Events.AddHandler(DomMouseOverEvent, value); }
			remove { this.Events.RemoveHandler(DomMouseOverEvent, value); }
		}
		private static object DomMouseOverEvent = new object();

		/// <summary>Raises the <see cref="DomMouseOver"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnDomMouseOver(GeckoDomMouseEventArgs e)
		{
			if (((GeckoDomMouseEventHandler)this.Events[DomMouseOverEvent]) != null)
				((GeckoDomMouseEventHandler)this.Events[DomMouseOverEvent])(this, e);
		}
		#endregion

		#region public event GeckoDomMouseEventHandler DomMouseOut
		[Category("DOM Events")]
		public event GeckoDomMouseEventHandler DomMouseOut
		{
			add { this.Events.AddHandler(DomMouseOutEvent, value); }
			remove { this.Events.RemoveHandler(DomMouseOutEvent, value); }
		}
		private static object DomMouseOutEvent = new object();

		/// <summary>Raises the <see cref="DomMouseOut"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnDomMouseOut(GeckoDomMouseEventArgs e)
		{
			if (((GeckoDomMouseEventHandler)this.Events[DomMouseOutEvent]) != null)
				((GeckoDomMouseEventHandler)this.Events[DomMouseOutEvent])(this, e);
		}
		#endregion

		#region public event GeckoDomMouseEventHandler DomMouseMove
		[Category("DOM Events")]
		public event GeckoDomMouseEventHandler DomMouseMove
		{
			add { this.Events.AddHandler(DomMouseMoveEvent, value); }
			remove { this.Events.RemoveHandler(DomMouseMoveEvent, value); }
		}
		private static object DomMouseMoveEvent = new object();

		/// <summary>Raises the <see cref="DomMouseMove"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnDomMouseMove(GeckoDomMouseEventArgs e)
		{
			if (((GeckoDomMouseEventHandler)this.Events[DomMouseMoveEvent]) != null)
				((GeckoDomMouseEventHandler)this.Events[DomMouseMoveEvent])(this, e);
		}
		#endregion
		
		#endregion

		#region nsISHistoryListener Members

		#region public event GeckoHistoryEventHandler HistoryNewEntry
		[Category("History")]
		public event GeckoHistoryEventHandler HistoryNewEntry
		{
			add { this.Events.AddHandler(HistoryNewEntryEvent, value); }
			remove { this.Events.RemoveHandler(HistoryNewEntryEvent, value); }
		}
		private static object HistoryNewEntryEvent = new object();

		/// <summary>Raises the <see cref="HistoryNewEntry"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnHistoryNewEntry(GeckoHistoryEventArgs e)
		{
			if (((GeckoHistoryEventHandler)this.Events[HistoryNewEntryEvent]) != null)
				((GeckoHistoryEventHandler)this.Events[HistoryNewEntryEvent])(this, e);
		}
		#endregion

		#region public event GeckoHistoryEventHandler HistoryGoBack
		[Category("History")]
		public event GeckoHistoryEventHandler HistoryGoBack
		{
			add { this.Events.AddHandler(HistoryGoBackEvent, value); }
			remove { this.Events.RemoveHandler(HistoryGoBackEvent, value); }
		}
		private static object HistoryGoBackEvent = new object();

		/// <summary>Raises the <see cref="HistoryGoBack"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnHistoryGoBack(GeckoHistoryEventArgs e)
		{
			if (((GeckoHistoryEventHandler)this.Events[HistoryGoBackEvent]) != null)
				((GeckoHistoryEventHandler)this.Events[HistoryGoBackEvent])(this, e);
		}
		#endregion

		#region public event GeckoHistoryEventHandler HistoryGoForward
		[Category("History")]
		public event GeckoHistoryEventHandler HistoryGoForward
		{
			add { this.Events.AddHandler(HistoryGoForwardEvent, value); }
			remove { this.Events.RemoveHandler(HistoryGoForwardEvent, value); }
		}
		private static object HistoryGoForwardEvent = new object();

		/// <summary>Raises the <see cref="HistoryGoForward"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnHistoryGoForward(GeckoHistoryEventArgs e)
		{
			if (((GeckoHistoryEventHandler)this.Events[HistoryGoForwardEvent]) != null)
				((GeckoHistoryEventHandler)this.Events[HistoryGoForwardEvent])(this, e);
		}
		#endregion

		#region public event GeckoHistoryEventHandler HistoryReload
		[Category("History")]
		public event GeckoHistoryEventHandler HistoryReload
		{
			add { this.Events.AddHandler(HistoryReloadEvent, value); }
			remove { this.Events.RemoveHandler(HistoryReloadEvent, value); }
		}
		private static object HistoryReloadEvent = new object();

		/// <summary>Raises the <see cref="HistoryReload"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnHistoryReload(GeckoHistoryEventArgs e)
		{
			if (((GeckoHistoryEventHandler)this.Events[HistoryReloadEvent]) != null)
				((GeckoHistoryEventHandler)this.Events[HistoryReloadEvent])(this, e);
		}
		#endregion

		#region public event GeckoHistoryGotoIndexEventHandler HistoryGotoIndex
		[Category("History")]
		public event GeckoHistoryGotoIndexEventHandler HistoryGotoIndex
		{
			add { this.Events.AddHandler(HistoryGotoIndexEvent, value); }
			remove { this.Events.RemoveHandler(HistoryGotoIndexEvent, value); }
		}
		private static object HistoryGotoIndexEvent = new object();

		/// <summary>Raises the <see cref="HistoryGotoIndex"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnHistoryGotoIndex(GeckoHistoryGotoIndexEventArgs e)
		{
			if (((GeckoHistoryGotoIndexEventHandler)this.Events[HistoryGotoIndexEvent]) != null)
				((GeckoHistoryGotoIndexEventHandler)this.Events[HistoryGotoIndexEvent])(this, e);
		}
		#endregion

		#region public event GeckoHistoryPurgeEventHandler HistoryPurge
		[Category("History")]
		public event GeckoHistoryPurgeEventHandler HistoryPurge
		{
			add { this.Events.AddHandler(HistoryPurgeEvent, value); }
			remove { this.Events.RemoveHandler(HistoryPurgeEvent, value); }
		}
		private static object HistoryPurgeEvent = new object();

		/// <summary>Raises the <see cref="HistoryPurge"/> event.</summary>
		/// <param name="e">The data for the event.</param>
		protected virtual void OnHistoryPurge(GeckoHistoryPurgeEventArgs e)
		{
			if (((GeckoHistoryPurgeEventHandler)this.Events[HistoryPurgeEvent]) != null)
				((GeckoHistoryPurgeEventHandler)this.Events[HistoryPurgeEvent])(this, e);
		}
		#endregion
		
		void nsISHistoryListener.OnHistoryNewEntry(nsIURI aNewURI)
		{
			OnHistoryNewEntry(new GeckoHistoryEventArgs(new Uri(nsString.Get(aNewURI.GetSpec))));
		}

		bool nsISHistoryListener.OnHistoryGoBack(nsIURI aBackURI)
		{
			GeckoHistoryEventArgs e = new GeckoHistoryEventArgs(new Uri(nsString.Get(aBackURI.GetSpec)));
			OnHistoryGoBack(e);
			return !e.Cancel;
		}

		bool nsISHistoryListener.OnHistoryGoForward(nsIURI aForwardURI)
		{
			GeckoHistoryEventArgs e = new GeckoHistoryEventArgs(new Uri(nsString.Get(aForwardURI.GetSpec)));
			OnHistoryGoForward(e);
			return !e.Cancel;
		}

		bool nsISHistoryListener.OnHistoryReload(nsIURI aReloadURI, uint aReloadFlags)
		{
			GeckoHistoryEventArgs e = new GeckoHistoryEventArgs(new Uri(nsString.Get(aReloadURI.GetSpec)));
			OnHistoryReload(e);
			return !e.Cancel;
		}

		bool nsISHistoryListener.OnHistoryGotoIndex(int aIndex, nsIURI aGotoURI)
		{
			GeckoHistoryGotoIndexEventArgs e = new GeckoHistoryGotoIndexEventArgs(new Uri(nsString.Get(aGotoURI.GetSpec)), aIndex);
			OnHistoryGotoIndex(e);
			return !e.Cancel;
		}

		bool nsISHistoryListener.OnHistoryPurge(int aNumEntries)
		{
			GeckoHistoryPurgeEventArgs e = new GeckoHistoryPurgeEventArgs(aNumEntries);
			OnHistoryPurge(e);
			return !e.Cancel;
		}

		#endregion
	}
	
	#region public delegate void GeckoHistoryEventHandler(object sender, GeckoHistoryEventArgs e);
	public delegate void GeckoHistoryEventHandler(object sender, GeckoHistoryEventArgs e);

	/// <summary>Provides data for the <see cref="GeckoHistoryEventHandler"/> event.</summary>
	public class GeckoHistoryEventArgs : System.EventArgs
	{
		/// <summary>Creates a new instance of a <see cref="GeckoHistoryEventArgs"/> object.</summary>
		/// <param name="url"></param>
		public GeckoHistoryEventArgs(Uri url)
		{
			_Url = url;
		}
		
		/// <summary>
		/// Gets the URL of the history entry.
		/// </summary>
		public Uri Url { get { return _Url; } }
		Uri _Url;
		
		/// <summary>
		/// Gets or sets whether the history action should be cancelled.
		/// </summary>
		public bool Cancel
		{
			get { return _Cancel; }
			set { _Cancel = value; }
		}
		bool _Cancel;
	}
	#endregion
	
	#region public delegate void GeckoHistoryGotoIndexEventHandler(object sender, GeckoHistoryGotoIndexEventArgs e);
	public delegate void GeckoHistoryGotoIndexEventHandler(object sender, GeckoHistoryGotoIndexEventArgs e);

	/// <summary>Provides data for the <see cref="GeckoHistoryGotoIndexEventHandler"/> event.</summary>
	public class GeckoHistoryGotoIndexEventArgs : GeckoHistoryEventArgs
	{
		/// <summary>Creates a new instance of a <see cref="GeckoHistoryGotoIndexEventArgs"/> object.</summary>
		/// <param name="url"></param>
		public GeckoHistoryGotoIndexEventArgs(Uri url, int index) : base(url)
		{
			_Index = index;
		}
		
		/// <summary>
		/// Gets the index in history of the document to be loaded.
		/// </summary>
		public int Index
		{
			get { return _Index; }
		}
		int _Index;
	}
	#endregion
	
	#region public delegate void GeckoHistoryPurgeEventHandler(object sender, GeckoHistoryPurgeEventArgs e);
	public delegate void GeckoHistoryPurgeEventHandler(object sender, GeckoHistoryPurgeEventArgs e);

	/// <summary>Provides data for the <see cref="GeckoHistoryPurgeEventHandler"/> event.</summary>
	public class GeckoHistoryPurgeEventArgs : CancelEventArgs
	{
		/// <summary>Creates a new instance of a <see cref="GeckoHistoryPurgeEventArgs"/> object.</summary>
		/// <param name="count"></param>
		public GeckoHistoryPurgeEventArgs(int count)
		{
			_Count = count;
		}
		
		/// <summary>
		/// Gets the number of entries to be purged from the history.
		/// </summary>
		public int Count { get { return _Count; } }
		int _Count;
	}
	#endregion
	
	#region public delegate void GeckoProgressEventHandler(object sender, GeckoProgressEventArgs e);
	public delegate void GeckoProgressEventHandler(object sender, GeckoProgressEventArgs e);

	/// <summary>Provides data for the <see cref="GeckoProgressEventHandler"/> event.</summary>
	public class GeckoProgressEventArgs : System.EventArgs
	{
		/// <summary>Creates a new instance of a <see cref="GeckoProgressEventArgs"/> object.</summary>
		public GeckoProgressEventArgs(int current, int max)
		{
			_CurrentProgress = current;
			_MaximumProgress = max;
		}
		
		public int CurrentProgress { get { return _CurrentProgress; } }
		int _CurrentProgress;
		
		public int MaximumProgress { get { return _MaximumProgress; } }
		int _MaximumProgress;
	}
	#endregion
	
	#region public delegate void GeckoNavigatedEventHandler(object sender, GeckoNavigatedEventArgs e);
	public delegate void GeckoNavigatedEventHandler(object sender, GeckoNavigatedEventArgs e);

	/// <summary>Provides data for the <see cref="GeckoNavigatedEventHandler"/> event.</summary>
	public class GeckoNavigatedEventArgs : System.EventArgs
	{
		/// <summary>Creates a new instance of a <see cref="GeckoNavigatedEventArgs"/> object.</summary>
		/// <param name="value"></param>
		public GeckoNavigatedEventArgs(Uri value)
		{
			_Uri = value;
		}

		public Uri Uri { get { return _Uri; } }
		Uri _Uri;
	}
	#endregion
	
	#region public delegate void GeckoNavigatingEventHandler(object sender, GeckoNavigatingEventArgs e);
	public delegate void GeckoNavigatingEventHandler(object sender, GeckoNavigatingEventArgs e);

	/// <summary>Provides data for the <see cref="GeckoNavigatingEventHandler"/> event.</summary>
	public class GeckoNavigatingEventArgs : System.ComponentModel.CancelEventArgs
	{
		/// <summary>Creates a new instance of a <see cref="GeckoNavigatingEventArgs"/> object.</summary>
		/// <param name="value"></param>
		public GeckoNavigatingEventArgs(Uri value)
		{
			_Uri = value;
		}

		public Uri Uri { get { return _Uri; } }
		Uri _Uri;
	}
	#endregion
	
	#region public delegate void GeckoCreateWindowEventHandler(object sender, GeckoCreateWindowEventArgs e);
	public delegate void GeckoCreateWindowEventHandler(object sender, GeckoCreateWindowEventArgs e);

	/// <summary>Provides data for the <see cref="GeckoCreateWindowEventHandler"/> event.</summary>
	public class GeckoCreateWindowEventArgs : System.EventArgs
	{
		/// <summary>Creates a new instance of a <see cref="GeckoCreateWindowEventArgs"/> object.</summary>
		/// <param name="flags"></param>
		public GeckoCreateWindowEventArgs(GeckoWindowFlags flags)
		{
			_Flags = flags;
		}
		
		public GeckoWindowFlags Flags { get { return _Flags; } }
		GeckoWindowFlags _Flags;
		
		/// <summary>
		/// Gets or sets the <see cref="GeckoWebBrowser"/> used in the new window.
		/// </summary>
		public GeckoWebBrowser WebBrowser
		{
			get { return _WebBrowser; }
			set { _WebBrowser = value; }
		}
		GeckoWebBrowser _WebBrowser;
	}
	#endregion
	
	#region public delegate void GeckoWindowSetSizeEventHandler(object sender, GeckoWindowSetSizeEventArgs e);
	public delegate void GeckoWindowSetSizeEventHandler(object sender, GeckoWindowSetSizeEventArgs e);

	/// <summary>Provides data for the <see cref="GeckoWindowSetSizeEventHandler"/> event.</summary>
	public class GeckoWindowSetSizeEventArgs : System.EventArgs
	{
		/// <summary>Creates a new instance of a <see cref="GeckoWindowSetSizeEventArgs"/> object.</summary>
		/// <param name="windowSize"></param>
		public GeckoWindowSetSizeEventArgs(Size windowSize)
		{
			_WindowSize = windowSize;
		}

		public Size WindowSize { get { return _WindowSize; } }
		Size _WindowSize;
	}
	#endregion
	
	#region public enum GeckoLoadFlags
	public enum GeckoLoadFlags
	{
		/// <summary>
		/// This is the default value for the load flags parameter.
		/// </summary>
		None = nsIWebNavigationConstants.LOAD_FLAGS_NONE,

		/// <summary>
		/// This flag specifies that the load should have the semantics of an HTML
		/// META refresh (i.e., that the cache should be validated).  This flag is
		/// only applicable to loadURI.
		/// XXX the meaning of this flag is poorly defined.
		/// </summary>
		IsRefresh = nsIWebNavigationConstants.LOAD_FLAGS_IS_REFRESH,

		/// <summary>
		/// This flag specifies that the load should have the semantics of a link
		/// click.  This flag is only applicable to loadURI.
		/// XXX the meaning of this flag is poorly defined.
		/// </summary>
		IsLink = nsIWebNavigationConstants.LOAD_FLAGS_IS_LINK,

		/// <summary>
		/// This flag specifies that history should not be updated.  This flag is only
		/// applicable to loadURI.
		/// </summary>
		BypassHistory = nsIWebNavigationConstants.LOAD_FLAGS_BYPASS_HISTORY,

		/// <summary>
		/// This flag specifies that any existing history entry should be replaced.
		/// This flag is only applicable to loadURI.
		/// </summary>
		ReplaceHistory = nsIWebNavigationConstants.LOAD_FLAGS_REPLACE_HISTORY,

		/// <summary>
		/// This flag specifies that the local web cache should be bypassed, but an
		/// intermediate proxy cache could still be used to satisfy the load.
		/// </summary>
		BypassCache = nsIWebNavigationConstants.LOAD_FLAGS_BYPASS_CACHE,

		/// <summary>
		/// This flag specifies that any intermediate proxy caches should be bypassed
		/// (i.e., that the content should be loaded from the origin server).
		/// </summary>
		BypassProxy = nsIWebNavigationConstants.LOAD_FLAGS_BYPASS_PROXY,

		/// <summary>
		/// This flag specifies that a reload was triggered as a result of detecting
		/// an incorrect character encoding while parsing a previously loaded
		/// document.
		/// </summary>
		CharsetChange = nsIWebNavigationConstants.LOAD_FLAGS_CHARSET_CHANGE,

		/// <summary>
		/// If this flag is set, Stop() will be called before the load starts
		/// and will stop both content and network activity (the default is to
		/// only stop network activity).  Effectively, this passes the
		/// STOP_CONTENT flag to Stop(), in addition to the STOP_NETWORK flag.
		/// </summary>
		StopContent = nsIWebNavigationConstants.LOAD_FLAGS_STOP_CONTENT,

		/// <summary>
		/// A hint this load was prompted by an external program: take care!
		/// </summary>
		FromExternal = nsIWebNavigationConstants.LOAD_FLAGS_FROM_EXTERNAL,

		/// <summary>
		/// This flag specifies that the URI may be submitted to a third-party
		/// server for correction. This should only be applied to non-sensitive
		/// URIs entered by users.
		/// </summary>
		AllowThirdPartyFixup = nsIWebNavigationConstants.LOAD_FLAGS_ALLOW_THIRD_PARTY_FIXUP,

		/// <summary>
		/// This flag specifies that this is the first load in this object.
		/// Set with care, since setting incorrectly can cause us to assume that
		/// nothing was actually loaded in this object if the load ends up being
		/// handled by an external application.
		/// </summary>
		FirstLoad = nsIWebNavigationConstants.LOAD_FLAGS_FIRST_LOAD,
	}
	#endregion
	
	#region public enum GeckoSecurityState
	public enum GeckoSecurityState
	{
		/// <summary>
		/// This flag indicates that the data corresponding to the request was received over an insecure channel.
		/// </summary>
		Insecure = nsIWebProgressListenerConstants.STATE_IS_INSECURE,
		/// <summary>
		/// This flag indicates an unknown security state.  This may mean that the request is being loaded as part of
		/// a page in which some content was received over an insecure channel.
		/// </summary>
		Broken = nsIWebProgressListenerConstants.STATE_IS_BROKEN,
		/// <summary>
		/// This flag indicates that the data corresponding to the request was received over a secure channel.
		/// The degree of security is expressed by GeckoSecurityStrength.
		/// </summary>
		Secure = nsIWebProgressListenerConstants.STATE_IS_SECURE,
	}
	#endregion
	
	#region public enum GeckoWindowFlags
	[Flags]
	public enum GeckoWindowFlags
	{
		Default = 0x1,
		WindowBorders = 0x2,
		WindowClose = 0x4,
		WindowResize = 0x8,
		MenuBar = 0x10,
		ToolBar = 0x20,
		LocationBar = 0x40,
		StatusBar = 0x80,
		PersonalToolbar = 0x100,
		ScrollBars = 0x200,
		TitleBar = 0x400,
		Extra = 0x800,
		
		CreateWithSize = 0x1000,
		CreateWithPosition = 0x2000,
		
		WindowMin = 0x00004000,
		WindowPopup = 0x00008000,
		WindowRaised = 0x02000000,
		WindowLowered = 0x04000000,
		CenterScreen = 0x08000000,
		Dependent = 0x10000000,
		Modal = 0x20000000,
		OpenAsDialog = 0x40000000,
		OpenAsChrome = unchecked((int)0x80000000),
		All = 0x00000ffe,
	}
	#endregion
}
