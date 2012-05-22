package de.devsurf.azure.tools;

import java.io.IOException;
import java.io.OutputStream;
import java.io.PrintStream;

import cli.System.Diagnostics.Trace;

public class SystemChannels {
	public static void channelSystemOut(){
		System.setOut(new PrintStream(new DotNetOutputStream()));
	}
	
	public static void channelSystemErr(){
		System.setErr(new PrintStream(new DotNetOutputStream()));
	}
	
	public static class DotNetOutputStream extends OutputStream{
		public DotNetOutputStream() {
			super();
		}
		
		@Override
		public void write(byte[] b) throws IOException {
			Trace.Write(new String(b));
		}
		
		@Override
		public void write(int b) throws IOException {
			write(new byte[]{(byte) b});
		}
	}
}
