package de.devsurf.azure.tools;

import cli.System.Diagnostics.Trace;

public class LogActions {
	public static void info(String message){
		Trace.TraceInformation(message);
	}
	
	public static void warn(String message){
		Trace.TraceWarning(message);
	}
	
	public static void error(String message){
		Trace.TraceError(message);
	}
}
