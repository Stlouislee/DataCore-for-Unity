using System.Collections.Generic;
using AroAro.DataCore.Import;
using Xunit;

namespace DataCore.Tests
{
    public class CsvParserTests
    {
        [Fact]
        public void ParseLine_SimpleFields_SplitsCorrectly()
        {
            var result = CsvParser.ParseLine("a,b,c");
            Assert.Equal(3, result.Count);
            Assert.Equal("a", result[0]);
            Assert.Equal("b", result[1]);
            Assert.Equal("c", result[2]);
        }

        [Fact]
        public void ParseLine_QuotedFieldWithDelimiter_PreservesDelimiter()
        {
            var result = CsvParser.ParseLine("Alice,\"Software engineer, data scientist\",NYC");
            Assert.Equal(3, result.Count);
            Assert.Equal("Alice", result[0]);
            Assert.Equal("Software engineer, data scientist", result[1]);
            Assert.Equal("NYC", result[2]);
        }

        [Fact]
        public void ParseLine_EscapedQuotes_UnescapesCorrectly()
        {
            var result = CsvParser.ParseLine("\"She said \"\"hello\"\"\",b");
            Assert.Equal(2, result.Count);
            Assert.Equal("She said \"hello\"", result[0]);
            Assert.Equal("b", result[1]);
        }

        [Fact]
        public void ParseLine_EmptyFields_Preserved()
        {
            var result = CsvParser.ParseLine("a,,c,");
            Assert.Equal(4, result.Count);
            Assert.Equal("a", result[0]);
            Assert.Equal("", result[1]);
            Assert.Equal("c", result[2]);
            Assert.Equal("", result[3]);
        }

        [Fact]
        public void ParseLine_CustomDelimiter_Works()
        {
            var result = CsvParser.ParseLine("a;b;c", ';');
            Assert.Equal(3, result.Count);
            Assert.Equal("a", result[0]);
        }

        [Fact]
        public void ParseLine_QuotedFieldWithNewline_OnlyInParseAll()
        {
            // ParseLine handles single lines; newlines in quotes require ParseAll
            var result = CsvParser.ParseLine("\"hello\",world");
            Assert.Equal(2, result.Count);
            Assert.Equal("hello", result[0]);
            Assert.Equal("world", result[1]);
        }

        [Fact]
        public void ParseAll_SimpleCsv_ParsesCorrectly()
        {
            var csv = "Name,Age\nAlice,30\nBob,25";
            var rows = CsvParser.ParseAll(csv);
            Assert.Equal(3, rows.Count);
            Assert.Equal("Alice", rows[1][0]);
            Assert.Equal("30", rows[1][1]);
        }

        [Fact]
        public void ParseAll_QuotedFieldWithDelimiter_PreservesContent()
        {
            var csv = "Name,Bio\nAlice,\"Software engineer, data scientist\"\nBob,Developer";
            var rows = CsvParser.ParseAll(csv);
            Assert.Equal(3, rows.Count);
            Assert.Equal("Alice", rows[1][0]);
            Assert.Equal("Software engineer, data scientist", rows[1][1]);
            Assert.Equal("Bob", rows[2][0]);
            Assert.Equal("Developer", rows[2][1]);
        }

        [Fact]
        public void ParseAll_QuotedFieldWithNewline_SingleField()
        {
            var csv = "Name,Bio\nAlice,\"Line1\nLine2\"\nBob,Dev";
            var rows = CsvParser.ParseAll(csv);
            Assert.Equal(3, rows.Count);
            Assert.Equal("Alice", rows[1][0]);
            Assert.Equal("Line1\nLine2", rows[1][1]);
        }

        [Fact]
        public void ParseAll_EscapedQuotesInQuotedField()
        {
            var csv = "Name,Quote\nAlice,\"She said \"\"hello\"\"\"\nBob,Hi";
            var rows = CsvParser.ParseAll(csv);
            Assert.Equal(3, rows.Count);
            Assert.Equal("She said \"hello\"", rows[1][1]);
        }

        [Fact]
        public void ParseAll_CRLF_LineEndings()
        {
            var csv = "A,B\r\n1,2\r\n3,4";
            var rows = CsvParser.ParseAll(csv);
            Assert.Equal(3, rows.Count);
            Assert.Equal("1", rows[1][0]);
            Assert.Equal("4", rows[2][1]);
        }

        [Fact]
        public void ParseAll_EmptyCsv_ReturnsEmpty()
        {
            var rows = CsvParser.ParseAll("");
            Assert.Empty(rows);
        }

        [Fact]
        public void ParseAll_SingleRow_ReturnsOneRow()
        {
            var rows = CsvParser.ParseAll("a,b,c");
            Assert.Single(rows);
            Assert.Equal(3, rows[0].Count);
        }

        [Fact]
        public void ParseAll_CustomDelimiter_Semicolon()
        {
            var csv = "A;B\n1;2";
            var rows = CsvParser.ParseAll(csv, ';');
            Assert.Equal(2, rows.Count);
            Assert.Equal("1", rows[1][0]);
            Assert.Equal("2", rows[1][1]);
        }

        [Fact]
        public void ParseAll_MixedQuotedAndUnquoted()
        {
            var csv = "Name,Value\nAlice,100\n\"Bob, Jr\",200";
            var rows = CsvParser.ParseAll(csv);
            Assert.Equal(3, rows.Count);
            Assert.Equal("Bob, Jr", rows[2][0]);
            Assert.Equal("200", rows[2][1]);
        }
    }
}
