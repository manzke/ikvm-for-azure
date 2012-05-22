package de.devsurf.azure;

import java.util.List;
import java.util.Map;

import cli.System.Diagnostics.Process;
import cli.System.Diagnostics.ProcessStartInfo;
import de.devsurf.azure.tools.Settings;
import de.devsurf.azure.tools.Settings.Setting;

public class TomcatRole extends IkvmRole {
	protected Process process;
	
	public void configure(Map<String, String> settings){
		info(TomcatRole.class.getName()+" - configure");
		process = new Process();
		process.set_EnableRaisingEvents(true);
		
		ProcessStartInfo startInfo = new ProcessStartInfo();
		startInfo.set_UseShellExecute(false);
		startInfo.set_RedirectStandardError(true);
		startInfo.set_RedirectStandardOutput(true);
		startInfo.set_Arguments("");
		startInfo.set_FileName("");
		
		
		/*
		 * WorkingDirectory = SettingsNames.TomcatFolder,
                        FileName = relToTomcat(jdkFolder, @"bin\java.exe"),
                        Arguments = arguments.ToString(),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
		 */
		process.set_StartInfo(startInfo);
	}
	
	public boolean startup(){
		info(TomcatRole.class.getName()+" - startup");
		return true;
	}
	
	public void shutdown(){
		warn(TomcatRole.class.getName()+" - going down.");
	}
	
	@Override
	public List<Setting> settings() {
		return Settings.create("TomcatFolderPath", false).build();
	}
}
