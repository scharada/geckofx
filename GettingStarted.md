# Introduction #

GeckoFX is a .NET wrapper around XULRunner, a runtime based on the same source
code as Firefox.  You can add the control to your windows forms app and use it much the
same way as System.Windows.Forms.WebBrowser.

Since GeckoFX is a wrapper, you need to the XULRunner runtime somewhere on your
development system (and redistribute it with your application).  GeckoFX now works best
with XULRunner 1.9 (Firefox 3).

(1) Download XULRunner 1.9 from:

> [ftp://ftp.mozilla.org/pub/xulrunner/releases/1.9.0.0/runtimes/xulrunner-1.9.en-US.win32.zip](ftp://ftp.mozilla.org/pub/xulrunner/releases/1.9.0.0/runtimes/xulrunner-1.9.en-US.win32.zip)

(2) In your application startup code, call:

```
Skybound.Gecko.Xpcom.Initialize(xulrunnerPath);
```

where "xulrunnerPath" is the full path to the location where you extracted the "xulrunner" directory
(containing xul.dll, xpcom.dll, etc).

(3) OPTIONAL: Specify a profile directory by setting Xpcom.ProfileDirectory.

(4) OPTIONAL: There are some files included with XULRunner which GeckoFX doesn't need.  You may
safely delete them:

```
AccessibleMarshal.dll
dependentlibs.list
mozctl.dll
mozctlx.dll
java*.*
*.ini
*.txt
*.exe
```

(5) OPTIONAL:  XULRunner does not support about:config out of the box.  If you want to provide access to this configuration page, copy the files from the "chrome" directory that came with GeckoFX into the "chrome" directory in your XULRunner path.

The files that need to be copied are "geckofx.jar" and "geckofx.manifest".