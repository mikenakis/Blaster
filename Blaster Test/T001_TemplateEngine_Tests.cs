namespace Blaster_Test;

using Blaster;
using Testing;
using static MikeNakis.Kit.GlobalStatics;
using VSTesting = Microsoft.VisualStudio.TestTools.UnitTesting;

[VSTesting.TestClass]
public class T001_TemplateEngine_Tests : TestClass
{
	[VSTesting.TestMethod]
	public void TemplateEngine_Works1()
	{
		TemplateEngine template = TemplateEngine.Create( "{{a}}" );
		string text = template.GenerateText( name => name switch
			{
				"a" => "X",
				_ => "?"
			} );
		Assert( text == "X" );
	}

	[VSTesting.TestMethod]
	public void TemplateEngine_Works2()
	{
		TemplateEngine template = TemplateEngine.Create( ">{{a}}<" );
		string text = template.GenerateText( name => name switch
			{
				"a" => "X",
				_ => "?"
			} );
		Assert( text == ">X<" );
	}

	[VSTesting.TestMethod]
	public void TemplateEngine_Works3()
	{
		TemplateEngine template = TemplateEngine.Create( ">{{a}}{{b}}<" );
		string text = template.GenerateText( name => name switch
			{
				"a" => "X",
				"b" => "Y",
				_ => "?"
			} );
		Assert( text == ">XY<" );
	}

	[VSTesting.TestMethod]
	public void TemplateEngine_Works4()
	{
		TemplateEngine template = TemplateEngine.Create( ">{{}}{{b}}<" );
		string text = template.GenerateText( name => name switch
			{
				"a" => "X",
				"b" => "Y",
				_ => "?"
			} );
		Assert( text == ">{{}}Y<" );
	}

	[VSTesting.TestMethod]
	public void TemplateEngine_Works5()
	{
		TemplateEngine template = TemplateEngine.Create( ">{{a{{b}}<" );
		string text = template.GenerateText( name => name switch
			{
				"a" => "X",
				"b" => "Y",
				_ => "?"
			} );
		Assert( text == ">{{aY<" );
	}

	[VSTesting.TestMethod]
	public void TemplateEngine_Works6()
	{
		TemplateEngine template = TemplateEngine.Create( ">{{a}}{{" );
		string text = template.GenerateText( name => name switch
			{
				"a" => "X",
				"b" => "Y",
				_ => "?"
			} );
		Assert( text == ">X{{" );
	}

	[VSTesting.TestMethod]
	public void TemplateEngine_Works7()
	{
		TemplateEngine template = TemplateEngine.Create( ">{{a}}{{b" );
		string text = template.GenerateText( name => name switch
			{
				"a" => "X",
				"b" => "Y",
				_ => "?"
			} );
		Assert( text == ">X{{b" );
	}
}
