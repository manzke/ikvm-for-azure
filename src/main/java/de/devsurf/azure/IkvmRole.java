package de.devsurf.azure;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

import cli.Microsoft.WindowsAzure.ServiceRuntime.RoleEntryPoint;
import cli.Microsoft.WindowsAzure.ServiceRuntime.RoleEnvironment;
import cli.Microsoft.WindowsAzure.ServiceRuntime.RoleEnvironmentChangingEventArgs;
import cli.System.EventHandler$$00601_$$$_Lcli__Microsoft__WindowsAzure__ServiceRuntime__RoleEnvironmentChangingEventArgs_$$$$_;
import cli.System.EventHandler$$00601_$$$_Lcli__Microsoft__WindowsAzure__ServiceRuntime__RoleEnvironmentChangingEventArgs_$$$$_.Method;
import cli.System.Collections.ObjectModel.ReadOnlyCollection$$00601_$$$_Lcli__Microsoft__WindowsAzure__ServiceRuntime__RoleEnvironmentChange_$$$$_;
import cli.System.Diagnostics.Trace;
import de.devsurf.azure.tools.IkvmInitializer;
import de.devsurf.azure.tools.Settings.Setting;

public abstract class IkvmRole extends RoleEntryPoint {
	static {
		new IkvmInitializer();
	}

	public abstract boolean startup();

	public abstract void shutdown();

	public abstract void configure(Map<String, String> settings);
		
	public abstract List<Setting> settings();
	
	public void change(Map<String, String> settings){
		configure(settings);
	}

	@Override
	public boolean OnStart() {
		info("Startup in Java...");
		List<Setting> settings = settings();
		Map<String, String> configuration = new HashMap<String, String>();
		for(Setting setting : settings){
			String value = getSetting(setting.name);
			if(value == null || value.isEmpty()){
				if(setting.mandatory){
					error("Configuration for Setting ["+setting.name+"] was not specified.");
					return false;					
				}
				continue;
			}else{
				configuration.put(setting.name, value);
			}
		}
		configure(configuration);
		
		RoleEnvironment.add_Changing(new EventHandler$$00601_$$$_Lcli__Microsoft__WindowsAzure__ServiceRuntime__RoleEnvironmentChangingEventArgs_$$$$_(new Method(
				) {
			@Override
			public void Invoke(Object object, RoleEnvironmentChangingEventArgs event) {
				info("configuration changed");
				info("object: "+object+" type["+object.getClass().getName()+"]");
				List<Object> items = new ArrayList<Object>();
				ReadOnlyCollection$$00601_$$$_Lcli__Microsoft__WindowsAzure__ServiceRuntime__RoleEnvironmentChange_$$$$_ changes = event.get_Changes();
				int count = changes.get_Count();
				for(int i=0;i<count;i++){
					String item = changes.get_Item(i).toString();
					info("changed item: "+item);
					items.add(item);
				}
				event.set_Cancel(true);
			}
		}));
		
		if(startup()){
			return super.OnStart();	
		}
		return false;
	}

	@Override
	public void OnStop() {
		warn("Shuting down...");
		shutdown();
		super.OnStop();
	}

	@Override
	public void Run() {
		while (true) {
			error("Running...");
			cli.System.Threading.Thread.Sleep(10000);
		}
	}

	String getSetting(String key) {
		return RoleEnvironment.GetConfigurationSettingValue(key);
	}

	void info(String message) {
		Trace.TraceInformation(message);
	}

	void warn(String message) {
		Trace.TraceWarning(message);
	}

	void error(String message) {
		Trace.TraceError(message);
	}
}
