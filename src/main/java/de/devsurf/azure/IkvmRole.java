package de.devsurf.azure;

import cli.Microsoft.WindowsAzure.ServiceRuntime.RoleEntryPoint;
import cli.System.Diagnostics.Process;
import cli.System.Diagnostics.ProcessStartInfo;
import cli.System.Diagnostics.Trace;
import de.devsurf.azure.tools.IkvmInitializer;

public class IkvmRole extends RoleEntryPoint {
	static{
		new IkvmInitializer();
	}
	
	@Override
	public boolean OnStart() {
		System.out.println("Startup in Java...");
		Process p = new Process();
		p.set_EnableRaisingEvents(true);
		
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
		p.set_StartInfo(startInfo);
		return super.OnStart();
	}

	@Override
	public void OnStop() {
		System.out.println("Shuting down...");
		super.OnStop();
	}
	
	@Override
	public void Run() {
        while (true)
        {
        	System.err.println("Running...");	
            cli.System.Threading.Thread.Sleep(10000);
        }
	}
	
	
	void getConfiguration(){
		
	}
	
	void info(String message){
		Trace.TraceInformation(message);
	}
	
	void warn(String message){
		Trace.TraceWarning(message);
	}
	
	void error(String message){
		Trace.TraceError(message);
	}
}
