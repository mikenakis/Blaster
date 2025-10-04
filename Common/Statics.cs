namespace Common;

using Sys = System;
using SysCodeAnalysis = System.Diagnostics.CodeAnalysis;
using SysDiag = System.Diagnostics;

public static class Statics
{
	public static bool True => true;
	public static bool False => false;

	public static T Identity<T>( T value ) => value;

	[SysDiag.DebuggerNonUserCode]
	[SysDiag.DebuggerHidden, SysDiag.Conditional( "DEBUG" )]
	public static void Assert( [SysCodeAnalysis.DoesNotReturnIf( false )] bool condition ) //
	{
		if( condition )
			return;
		if( Breakpoint() )
			return;
		throw new Sys.Exception();
	}

	[SysDiag.DebuggerHidden]
	public static bool Breakpoint()
	{
		if( SysDiag.Debugger.IsAttached )
		{
			SysDiag.Debugger.Break();
			return true;
		}
		return false;
	}
}
