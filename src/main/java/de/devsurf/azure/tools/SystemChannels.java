package de.devsurf.azure.tools;

import java.io.IOException;
import java.io.OutputStream;
import java.io.PrintStream;

import cli.System.Diagnostics.Trace;

public class SystemChannels {
	public static void channelSystemOut(){
		System.setOut(new PrintStream(new DotNetInfoOutputStream()));
	}
	
	public static void channelSystemErr(){
		System.setErr(new PrintStream(new DotNetErrorOutputStream()));
	}
	
	public static class DotNetInfoOutputStream extends OutputStream{
		public DotNetInfoOutputStream() {
			super();
		}
		
		@Override
		public void write(byte[] b) throws IOException {
			Trace.TraceInformation(new String(b));
		}
		
		@Override
		public void write(int b) throws IOException {
			write(new byte[]{(byte) b});
		}
	}
	
	public static class DotNetErrorOutputStream extends OutputStream{
		public DotNetErrorOutputStream() {
			super();
		}
		
		@Override
		public void write(byte[] b) throws IOException {
			Trace.TraceError(new String(b));
		}
		
		@Override
		public void write(int b) throws IOException {
			write(new byte[]{(byte) b});
		}
	}
}
