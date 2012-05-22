package de.devsurf.azure.tools;

import java.util.ArrayList;
import java.util.List;

public class Settings {
	protected List<Setting> settings;
	
	public Settings add(String name, boolean mandatory){
		settings.add(Setting.create(name, mandatory));
		return this;
	}
	
	public List<Setting> build(){
		return settings;
	}
	
	public static Settings create(String name, boolean mandatory){
		Settings settings = new Settings();
		settings.settings = new ArrayList<Settings.Setting>();
		settings.settings.add(Setting.create(name, mandatory));
		
		return settings;
	}
	
	public static class Setting{
		public final boolean mandatory;
		public final String name;
		
		Setting(String name, boolean mandatory){
			this.mandatory = mandatory;
			this.name = name;
		}
		
		public static Setting create(String name, boolean mandatory){
			return new Setting(name, mandatory);
		}
	}
}
