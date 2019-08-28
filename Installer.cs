using Godot;
using System;
using MTGInstaller;
using Environment = System.Environment;
using Directory = System.IO.Directory;
using Path = System.IO.Path;
using System.Collections.Generic;
using System.Threading;
using Thread = System.Threading.Thread;
using System.Reflection;
using System.Net;
using System.Text;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

//https://stackoverflow.com/a/18727100
public class FieldWriter : System.IO.TextWriter {
	private object _Target;
	private string _Field;
	private Type _Type;
	private FieldInfo _FieldInfo;

	public FieldWriter(object obj, string field) {
		_Target = obj;
		_Field = field;
		_Type = obj.GetType();
		_FieldInfo = _Type.GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
	}

	public override void Write(char value) {
		var val = _FieldInfo.GetValue(_Target) as string;
		if (val == null) val = "";

		_FieldInfo.SetValue(_Target, $"{val}{value}");
	}

	public override void Write(string value) {
		var val = _FieldInfo.GetValue(_Target) as string;
		if (val == null) val = "";

		_FieldInfo.SetValue(_Target, $"{val}{value}");
	}

	public override System.Text.Encoding Encoding {
		get { return System.Text.Encoding.ASCII; }
	}
}

public class Installer : Control {
	public enum ViewType {
		Main,
		Advanced,
		HostController
	}

	public const string DISCORD_INVITE = "https://discord.gg/dVXqxNE";

	public static VBoxContainer CurrentInterface;

	public VBoxContainer MainInterface;
	public VBoxContainer AdvancedInterface;
	public VBoxContainer HostControllerInterface;

	public LineEdit GungeonPath;
	public MenuButton ArchitectureList;

	public Button InstallButton;
	public Button SettingsButton;
	public Button ExeFileButton;

	public LineEdit OptionComponent;
	public LineEdit OptionArchitecture;
	public CheckBox OptionShowLog;
	public CheckBox OptionForceHTTP;
	public CheckBox OptionSkipVersionCheck;
	public CheckBox OptionForceBackup;
	public CheckBox OptionLeavePatchDLLs;
	public CheckBox OptionOffline;

	public TextureRect LargeImage;
	public TextEdit InstallLog;

	public Label DoneLabel;
	public Label NewsContent;

	public Label InstallErrorLabel;
	public HBoxContainer InstallErrorButtons;
	public Button InstallErrorDiscordButton;
	public Button InstallErrorLogHasteButton;

	public FileDialog ExeFileDialog;
	public WindowDialog ETGModWarningDialog;

	public bool PathIsAutodetected = true;

	public bool InstallationDone = false;
	public string QueuedLogText = "";
	public Exception InstallException = null;

	public void InitializeGungeonPath() {
		var path = Settings.Instance.ExecutablePath;
		if (path == null) {
			path = Autodetector.ExePath ?? "";
			PathIsAutodetected = true;
			if (path != "") {
				Settings.Instance.ExecutablePath = path;
			}
		}
		GungeonPath.Text = path;
		GungeonPath.CaretPosition = path.Length;
	}

	public void UpdateView(VBoxContainer view) {
		CurrentInterface.Hide();
		view.Show();
		CurrentInterface = view;
	}

	public void InitializeAdvancedSettings() {
		var arch_popup = ArchitectureList.GetPopup();
		arch_popup.AddItem("Autodetect");
		arch_popup.AddItem("x86");
		arch_popup.AddItem("x86_64");

		ArchitectureList.GetPopup().Set("custom_fonts/font", ArchitectureList.Get("custom_fonts/font"));
		ArchitectureList.GetPopup().Connect("id_pressed", this, "_on_ArchList_id_pressed");

		var mtginstaller_settings = MTGInstaller.Settings.Instance;

		OptionForceHTTP.Pressed = mtginstaller_settings.ForceHTTP;
		OptionSkipVersionCheck.Pressed = mtginstaller_settings.SkipVersionChecks;
		OptionForceBackup.Pressed = mtginstaller_settings.ForceBackup;
		OptionLeavePatchDLLs.Pressed = mtginstaller_settings.LeavePatchDLLs;
		OptionOffline.Pressed = mtginstaller_settings.Offline;

		var settings = SemiInstaller.Settings.Instance;
		switch (settings.Architecture) {
		case null: 
			ArchitectureList.Text = arch_popup.GetItemText(0);
			break;
		case "x86":
			Autodetector.Architecture = Architecture.X86;
			ArchitectureList.Text = arch_popup.GetItemText(1);
			break;
		case "x86_64":
			Autodetector.Architecture = Architecture.X86_64;
			ArchitectureList.Text = arch_popup.GetItemText(2);
			break;
		default:
			ArchitectureList.Text = arch_popup.GetItemText(0);
			settings.Architecture = null;
			break;
		}

		OptionComponent.Text = settings.Component;
		OptionShowLog.Pressed = settings.ShowLog;
	}

	public class InstallThreadUserdata : Godot.Object {
		public InstallerFrontend Frontend;
		public string Component;
		public string ExePath;
	}
	public void InstallThread(InstallThreadUserdata userdata) {
		var inst = userdata.Frontend;

		var components = new List<ComponentInfo>();
		components.Add(new ComponentInfo(userdata.Component, null));
		try {
			inst.Install(components, userdata.ExePath);
		}
		catch (Exception e) {
			InstallException = e;
		}

		InstallationDone = true;
	}

	public void Install() {
		DoneLabel.Hide();

		var opts = OptionOffline.Pressed ? InstallerFrontend.InstallerOptions.Offline : InstallerFrontend.InstallerOptions.None;
		if (OptionForceBackup.Pressed) opts |= InstallerFrontend.InstallerOptions.ForceBackup;
		if (OptionForceHTTP.Pressed) opts |= InstallerFrontend.InstallerOptions.HTTP;
		if (OptionLeavePatchDLLs.Pressed) opts |= InstallerFrontend.InstallerOptions.LeavePatchDLLs;
		if (OptionSkipVersionCheck.Pressed) opts |= InstallerFrontend.InstallerOptions.SkipVersionChecks;

		var inst = new InstallerFrontend(opts);
		if (inst.HasETGModInstalled(GungeonPath.Text)) {
			ETGModWarningDialog.Popup_();
			return;
		}

		if (OptionShowLog.Pressed) {
			LargeImage.Hide();
			InstallLog.Show();
		}

		InstallButton.Disabled = true;
		SettingsButton.Disabled = true;
		ExeFileButton.Disabled = true;

		var data = new InstallThreadUserdata {
			Component = OptionComponent.Text,
			ExePath = GungeonPath.Text,
			Frontend = inst
		};

		InstallationDone = false;
		InstallException = null;

		InstallErrorLabel.Hide();
		InstallErrorButtons.Hide();

		var thread = new Thread(new ThreadStart(() => InstallThread(data)));
		thread.Start();
	}

	public void HandleInstallError(Exception e) {
		InstallErrorLabel.Show();
		InstallErrorButtons.Show();
		System.Console.WriteLine($"[{e.GetType().Name}]: {e.Message}\n{e.StackTrace}");
	}

	public string MakeLogHaste() {
		var log = InstallLog.Text;
		var data = Encoding.UTF8.GetBytes(log);

		var request = (HttpWebRequest)WebRequest.Create("https://hastebin.com/documents");
		request.Method = "POST";
		request.ContentType = "text/plain";
		request.ContentLength = data.Length;

		using (var stream = request.GetRequestStream()) {
			stream.Write(data, 0, data.Length);
		}

		var http_response = (HttpWebResponse)request.GetResponse();
		if (http_response.StatusCode != HttpStatusCode.OK) {
			InstallErrorLogHasteButton.Text = "Request to hastebin.com failed.";
			return null;
		}
		var response = new StreamReader(http_response.GetResponseStream()).ReadToEnd();
		var key = response.Replace("{\"key\":\"", "").Replace("\"}", "");

		return $"https://hastebin.com/{key}";
	}

	public string GetNews() {
		try {
            GD.Print("getting news");
			using (var wc = new System.Net.WebClient()) return wc.DownloadString("https://modthegungeon.eu/semi/news.txt");
		} catch (WebException e) {
			return $"Failed to get news - no internet? ({e.Message})";
		}
	}

    public override void _Ready() {
        MTGInstaller.Logger.Subscribe((logger, level, indent, str) => {
            GD.Print($"[{logger.ID} {level}] {str}");
        });
		ServicePointManager.ServerCertificateValidationCallback = _RemoteCertificateValidationCallback;
		
		MainInterface = GetNode<VBoxContainer>("MainPanel/ColumnBox/MainInterface");
		AdvancedInterface = GetNode<VBoxContainer>("MainPanel/ColumnBox/AdvancedInterface");
		HostControllerInterface = GetNode<VBoxContainer>("MainPanel/ColumnBox/HostControllerInterface");
		GungeonPath = GetNode<LineEdit>("MainPanel/ColumnBox/MainInterface/ExeSelect/LineEdit");

		InstallButton = GetNode<Button>("MainPanel/ColumnBox/MainInterface/Buttons/Install");
		SettingsButton = GetNode<Button>("MainPanel/ColumnBox/MainInterface/Buttons/Settings");
		ExeFileButton = GetNode<Button>("MainPanel/ColumnBox/MainInterface/ExeSelect/Button");

		LargeImage = GetNode<TextureRect>("MainPanel/ColumnBox/LeftPane/LargeImage");
		InstallLog = GetNode<TextEdit>("MainPanel/ColumnBox/LeftPane/InstallLog");

		InstallErrorLabel = GetNode<Label>("MainPanel/ColumnBox/LeftPane/ErrorLabel");
		InstallErrorButtons = GetNode<HBoxContainer>("MainPanel/ColumnBox/LeftPane/ErrorButtons");
		InstallErrorDiscordButton = GetNode<Button>("MainPanel/ColumnBox/LeftPane/ErrorButtons/Discord");
		InstallErrorLogHasteButton = GetNode<Button>("MainPanel/ColumnBox/LeftPane/ErrorButtons/LogHaste");

		OptionComponent = GetNode<LineEdit>("MainPanel/ColumnBox/AdvancedInterface/Settings/SettingsList/Component/Content");
		ArchitectureList = GetNode<MenuButton>("MainPanel/ColumnBox/AdvancedInterface/Settings/SettingsList/Architecture/ArchList");
		OptionShowLog = GetNode<CheckBox>("MainPanel/ColumnBox/AdvancedInterface/Settings/SettingsList/Log");
		OptionForceHTTP = GetNode<CheckBox>("MainPanel/ColumnBox/AdvancedInterface/Settings/SettingsList/ForceHTTP");
		OptionSkipVersionCheck = GetNode<CheckBox>("MainPanel/ColumnBox/AdvancedInterface/Settings/SettingsList/SkipVersionChecks");
		OptionForceBackup = GetNode<CheckBox>("MainPanel/ColumnBox/AdvancedInterface/Settings/SettingsList/ForceBackup");
		OptionLeavePatchDLLs = GetNode<CheckBox>("MainPanel/ColumnBox/AdvancedInterface/Settings/SettingsList/LeavePatchDLLs");
		OptionOffline = GetNode<CheckBox>("MainPanel/ColumnBox/AdvancedInterface/Settings/SettingsList/Offline");

		ExeFileDialog = GetNode<FileDialog>("ExeFileDialog");
		ETGModWarningDialog = GetNode<WindowDialog>("ETGModWarning");

		DoneLabel = GetNode<Label>("MainPanel/ColumnBox/MainInterface/DoneLabel");
		NewsContent = GetNode<Label>("MainPanel/ColumnBox/MainInterface/NewsContent");

		NewsContent.Text = GetNews();

		CurrentInterface = MainInterface;

		//Logger.Subscribe((logger, loglevel, indent, str) => {
		//	var logline = logger.String(loglevel, str, indent);
		//	QueuedLogText += $"{logline}\n";
		//});

		Console.SetOut(new FieldWriter(this, "QueuedLogText"));


		InitializeGungeonPath();
		InitializeAdvancedSettings();
    }

	public override void _Process(float delta) {
		if (QueuedLogText.Length > 0) {
			var log_text = QueuedLogText;
			QueuedLogText = "";
			InstallLog.Text += log_text;
			InstallLog.CursorSetLine(99999);
		}

		if (InstallationDone) {
			InstallationDone = false;
			InstallButton.Disabled = false;
			SettingsButton.Disabled = false;
			ExeFileButton.Disabled = false;

			if (InstallException != null) {
				HandleInstallError(InstallException);
			} else {
				DoneLabel.Show();
			}
		}
	}

	private void _on_Settings_pressed() {
		UpdateView(AdvancedInterface);
	}

	private void _on_OK_pressed() {
		UpdateView(MainInterface);
	}

	private void _on_ArchList_id_pressed(int id) {
		ArchitectureList.Text = ArchitectureList.GetPopup().GetItemText(id);
		switch (ArchitectureList.Text) {
			case "Autodetect": SemiInstaller.Settings.Instance.Architecture = null; break;
			default: SemiInstaller.Settings.Instance.Architecture = ArchitectureList.Text; break;
		}
		if (PathIsAutodetected) InitializeGungeonPath();
		SemiInstaller.Settings.Instance.Save();
	}

	private void _on_ComponentContent_text_changed(String new_text) {
		SemiInstaller.Settings.Instance.Component = new_text;
		SemiInstaller.Settings.Instance.Save();
	}

	private void _on_Log_toggled(bool button_pressed) {
		SemiInstaller.Settings.Instance.ShowLog = button_pressed;
		SemiInstaller.Settings.Instance.Save();
	}


	private void _on_ForceHTTP_toggled(bool button_pressed)
	{
		Settings.Instance.ForceHTTP = button_pressed;
		Settings.Instance.Save();
	}


	private void _on_SkipVersionChecks_toggled(bool button_pressed) {
		Settings.Instance.SkipVersionChecks = button_pressed;
		Settings.Instance.Save();
	}


	private void _on_ForceBackup_toggled(bool button_pressed) {
		Settings.Instance.ForceBackup = button_pressed;
		Settings.Instance.Save();
	}


	private void _on_LeavePatchDLLs_toggled(bool button_pressed) {
		Settings.Instance.LeavePatchDLLs = button_pressed;
		Settings.Instance.Save();
	}

	private void _on_Offline_toggled(bool button_pressed) {
		Settings.Instance.Offline = button_pressed;
		Settings.Instance.Save();
	}


	private void _on_ExePathButton_pressed() {
		var exe_path = GungeonPath.Text;
		if (exe_path != null && exe_path != "") {
			ExeFileDialog.SetCurrentDir(Path.GetDirectoryName(exe_path));
			ExeFileDialog.SetCurrentPath(exe_path);
			ExeFileDialog.SetCurrentFile(Path.GetFileName(exe_path));
			ExeFileDialog.Invalidate();
		}
		ExeFileDialog.Popup_();
	}

	private void _on_ExeFileDialog_file_selected(String path) {
		Settings.Instance.ExecutablePath = path;
		InitializeGungeonPath();
		Settings.Instance.Save();
	}

	private void _on_Install_pressed() {
		Install();
	}

	private void _on_LogHaste_pressed() {
		var haste = MakeLogHaste();
		if (haste == null) return;

		OS.SetClipboard(haste);
	}


	private void _on_Discord_pressed() {
		OS.ShellOpen(DISCORD_INVITE);
	}

	//https://stackoverflow.com/a/33391290
	private bool _RemoteCertificateValidationCallback(System.Object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
	    bool isOk = true;
	    // If there are errors in the certificate chain,
	    // look at each error to determine the cause.
	    if (sslPolicyErrors != SslPolicyErrors.None) {
	        for (int i=0; i<chain.ChainStatus.Length; i++) {
	            if (chain.ChainStatus[i].Status == X509ChainStatusFlags.RevocationStatusUnknown) {
	                continue;
	            }
	            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
	            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
	            chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan (0, 1, 0);
	            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
	            bool chainIsValid = chain.Build ((X509Certificate2)certificate);
	            if (!chainIsValid) {
	                isOk = false;
	                break;
	            }
	        }
	    }
	    return isOk;
	}
}









