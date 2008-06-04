
==================================================
GeckoFX
(C) 2008 Skybound Software. All Rights Reserved.
http://www.geckofx.org
==================================================


Getting Started
---------------
GeckoFX is a .NET wrapper around XULRunner 1.8, a runtime based on the same source
code as Firefox 2.x.  You can add the control to your windows forms app and use it much the
same way as System.Windows.Forms.WebBrowser.

Since GeckoFX is a wrapper, you need to already have XULRunner installed.  At the moment,
GeckoFX works best with XULRunner 1.8.1 (older versions haven't been tested, and 1.9 definately
does NOT work).  You can download XULRunner 1.8.1 from:

	ftp://ftp.mozilla.org/pub/xulrunner/releases/1.8.1.3/contrib/win32/

Then, in your application startup code, call:

	Skybound.Gecko.Xpcom.Initialize(xulrunnerPath);

where "xulrunnerPath" is the full path to the location where you extracted the "xulrunner" directory
(containing xul.dll, xpcom.dll, etc).

OPTIONAL: Specify a profile directory by setting Xpcom.ProfileDirectory.

NOTE: There are some files included with XULRunner which GeckoFX doesn't need.  You may
safely delete them:

AccessibleMarshal.dll
dependentlibs.list
mozctl.dll
mozctlx.dll
java*.*
*.txt
*.exe


Known Bugs
----------
- The right-click menu is still missing some standard features like cut, paste.

Changes in 1.8.1.4
------------------
- HTTPS is now supported.  Remember that if you disabled the PSM by removing "pipnss.dll" and "pipnss.xpt" from
your XULRunner "components" directory, you need to put those files back now to support HTTPS.
- Displaying an alert or navigating to an invalid page when the browser control was first loaded would cause it to crash
- Confirm dialog can't be resized any more
- Added a Response property to the Navigated event to get additional information about the HTTP response
- CreateWindow event works properly; handle it to allow javascript code to open new windows.  Also handle the
WindowSetBounds event to update and set the position & size of the new window.
- Added Cut, Copy, Paste, Undo, Redo methods to WebBrowser, plus CanCut, CanCopy etc.  Also added many of these to the context menu.
- Added ToolTips
- Added GetAttributeNS() and SetAttributeNS() methods to GeckoElement
- Added Xpcom.ProfileDirectory property to specify where the user profile & cache is stored

Changes in 1.8.1.3
------------------
- The Geckofx assembly is now signed with a strong name
- DocumentTitle wasn't being updated properly
- Added: GeckoStyleSheet.OwnerNode, GeckoRule.ParentStyleSheet, StyleRuleCollection.IsReadOnly
- Added: WebBrowser.SaveDocument, WebBrowser.History
- nsURI class makes it easier to interop with nsIURI parameters in mozilla interfaces (for anyone importing their own mozilla interfaces)
- Fixed various bugs

Changes in 1.8.1.2
------------------
- Fixed form submission (by disabling the PSM)
- Added lots of new properties to the DOM objects
- Updated the docs
- Page Properties and View Source use the target IFRAME when you right-click on one
- Various DOM bugs