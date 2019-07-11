using Xunit;

public class HTMinILTest
{
	[Fact]
	public void MinifyRemovesWhitespaceStrings()
	{
		HTMinIL h = new HTMinIL();

		Assert.Equal("", h.Minify(""));
		Assert.Equal("", h.Minify(" "));
		Assert.Equal("", h.Minify("\n"));
		Assert.Equal("", h.Minify("\r"));
		Assert.Equal("", h.Minify("\r\n"));
		Assert.Equal("", h.Minify("\t"));
		Assert.Equal("", h.Minify(" \t    \n "));
		Assert.Equal("", h.Minify("\n     \t"));
	}

	[Fact]
	public void MinifyDoesNotJoinWords()
	{
		HTMinIL h = new HTMinIL();

		Assert.Equal("word ", h.Minify("word "));
		Assert.Equal(" word", h.Minify(" word"));
		Assert.Equal("word word", h.Minify("word word"));
		Assert.Equal("word word ", h.Minify("word word "));
		Assert.Equal(" word word", h.Minify(" word word"));
		Assert.Equal("word word", h.Minify("word   word"));
		Assert.Equal("word word ", h.Minify("word   word   "));
		Assert.Equal(" word word", h.Minify("   word   word"));
		Assert.Equal("word word", h.Minify("word\nword"));
		Assert.Equal("word word ", h.Minify("word\nword\n"));
		Assert.Equal(" word word", h.Minify("\nword\nword"));
	}

	[Fact]
	public void MinifyDoesNotJoinNumbers()
	{
		HTMinIL h = new HTMinIL();

		Assert.Equal("123 ", h.Minify("123 "));
		Assert.Equal(" 123", h.Minify(" 123"));
		Assert.Equal("123 123", h.Minify("123 123"));
		Assert.Equal("123 123 ", h.Minify("123 123 "));
		Assert.Equal(" 123 123", h.Minify(" 123 123"));
		Assert.Equal("123 123", h.Minify("123   123"));
		Assert.Equal("123 123 ", h.Minify("123   123   "));
		Assert.Equal(" 123 123", h.Minify("   123   123"));
		Assert.Equal("123 123", h.Minify("123\n123"));
		Assert.Equal("123 123 ", h.Minify("123\n123\n"));
		Assert.Equal(" 123 123", h.Minify("\n123\n123"));
	}

	[Fact]
	public void MinifyJoinsAngularBrackets()
	{
		HTMinIL h = new HTMinIL();

		Assert.Equal("><", h.Minify("> <"));
		Assert.Equal("<", h.Minify(" <"));
		Assert.Equal(">", h.Minify("> "));
		Assert.Equal("><", h.Minify(">\n<"));
		Assert.Equal("<", h.Minify("\n<"));
		Assert.Equal(">", h.Minify(">\n"));
	}

	[Fact]
	public void MinifyShortensSelfClosingTags()
	{
		HTMinIL h = new HTMinIL();

		Assert.Equal("<br/>", h.Minify("<br />"));
	}

	[Fact (Skip = "Not implemented yet.")]
	public void MinifyEliminatesSpacesBetweenTagAttributeEqualSigns()
	{
		HTMinIL h = new HTMinIL();

		Assert.Equal("<a href=\"http://example.com/\"/>", h.Minify("<a href= \"http://example.com/\"/>"));
		Assert.Equal("<a href=\"http://example.com/\"/>", h.Minify("<a href =\"http://example.com/\"/>"));
		Assert.Equal("<a href=\"http://example.com/\"/>", h.Minify("<a href = \"http://example.com/\"/>"));
	}

	[Fact]
	public void MinifyDoesNotEliminatesSpacesAroundEqualSigns()
	{
		HTMinIL h = new HTMinIL();

		Assert.Equal("href= \"http://example.com/\"", h.Minify("href= \"http://example.com/\""));
		Assert.Equal("href =\"http://example.com/\"", h.Minify("href =\"http://example.com/\""));
		Assert.Equal("href = \"http://example.com/\"", h.Minify("href = \"http://example.com/\""));
	}

	[Fact]
	public void MinifyEliminatesHTMLComments()
	{
		HTMinIL h = new HTMinIL();

		Assert.Equal("", h.Minify("<!-- -->"));
		Assert.Equal("", h.Minify("<!--An HTML Comment-->"));
		Assert.Equal("", h.Minify("<!--An HTML Comment with a <tag></tag> -->"));
		Assert.Equal("", h.Minify("<!--An HTML Comment with a self closing <tag/> -->"));
	}

	[Fact]
	public void MinifyDoesNotEliminateIEConditionalComments()
	{
		HTMinIL h = new HTMinIL();

		Assert.Equal("<!--[if IE]><![endif]-->", h.Minify("<!--[if IE]><![endif]-->"));
		Assert.Equal("<!--[if IE]>Preserved<![endif]-->", h.Minify("<!--[if IE]>Preserved<![endif]-->"));
		Assert.Equal("<!--[if !IE]><!--><html><!--<![endif]-->", h.Minify("<!--[if !IE]><!--><html><!--<![endif]-->"));
	}
}