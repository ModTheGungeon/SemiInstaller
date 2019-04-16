using System;
using Godot;
using YamlDotNet.Serialization;
using Path = System.IO.Path;
using File = System.IO.File;
using System.Reflection;

namespace SemiInstaller {
	public class Settings {
		public const string SEMI_INSTALLER_SETTINGS_YML_NAME = "semi-installer-settings.yml";
		private const string DEFAULT_SETTINGS = "component: \"semi\"\narch: null\nshowlog: false";

		public static string SemiSettingsFile {
			get {
				return Path.Combine(MTGInstaller.Settings.SettingsDir, SEMI_INSTALLER_SETTINGS_YML_NAME);
			}
		}

		private static Settings _Instance;
		public static Settings Instance {
			get {
				if (_Instance != null) return _Instance;

				if (!File.Exists(SemiSettingsFile)) {
					using (var writer = File.CreateText(SemiSettingsFile))
					{
						writer.Write(DEFAULT_SETTINGS);
					}
				}

				return _Instance = MTGInstaller.SerializationHelper.Deserializer.Deserialize<Settings>(File.ReadAllText(SemiSettingsFile));
			}
		}

		public void Save() {
			using (var writer = File.CreateText(SemiSettingsFile)) {
				var ser = MTGInstaller.SerializationHelper.Serializer.Serialize(this);
				writer.Write(ser);
			}
		}

		[YamlMember(Alias = "component")]
		public string Component { get; set; }

		[YamlMember(Alias = "arch")]
		public string Architecture { get; set; }

		[YamlMember(Alias = "showlog")]
		public bool ShowLog { get; set; }
	}
}
