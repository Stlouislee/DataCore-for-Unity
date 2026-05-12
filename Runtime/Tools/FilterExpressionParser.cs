using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AroAro.DataCore.Tools
{
    /// <summary>
    /// 解析 filter 表达式字符串为 ITabularQuery 操作序列
    ///
    /// 支持的语法:
    ///   比较: age > 18, score >= 90, name == Alice, status != inactive
    ///   逻辑: AND, OR, NOT
    ///   字符串: city contains Shang, name starts with A
    ///   空值: email is null, email is not null
    ///   范围: age between 18 35
    ///   集合: city in Shanghai Beijing Guangzhou
    ///   括号: (age > 18 AND city == Shanghai) OR admin == true
    /// </summary>
    public static class FilterExpressionParser
    {
        /// <summary>
        /// 解析表达式并返回一个过滤函数，可用于内存数据集
        /// </summary>
        public static Func<Dictionary<string, object>, bool> Parse(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return _ => true;

            var tokens = Tokenize(expression);
            var parser = new Parser(tokens);
            var ast = parser.ParseExpression();
            return ast.ToPredicate();
        }

        /// <summary>
        /// 解析表达式并应用到 ITabularQuery（用于 LiteDB 查询）
        /// </summary>
        public static ITabularQuery ApplyTo(ITabularQuery query, string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return query;

            var tokens = Tokenize(expression);
            var parser = new Parser(tokens);
            var ast = parser.ParseExpression();
            return ast.ApplyTo(query);
        }

        #region Tokenizer

        private enum TokenType
        {
            Identifier,
            Number,
            String,
            Operator,     // >, >=, <, <=, ==, !=
            And,
            Or,
            Not,
            LParen,
            RParen,
            Between,
            In,
            IsNull,
            IsNotNull,
            Contains,
            StartsWith,
            EndsWith,
            Eof
        }

        private class Token
        {
            public TokenType Type { get; }
            public string Value { get; }

            public Token(TokenType type, string value)
            {
                Type = type;
                Value = value;
            }

            public override string ToString() => $"{Type}:{Value}";
        }

        private static List<Token> Tokenize(string expression)
        {
            var tokens = new List<Token>();
            var span = expression.AsSpan().Trim();
            int pos = 0;

            while (pos < span.Length)
            {
                // Skip whitespace
                while (pos < span.Length && char.IsWhiteSpace(span[pos]))
                    pos++;

                if (pos >= span.Length) break;

                char c = span[pos];

                // Parentheses
                if (c == '(') { tokens.Add(new Token(TokenType.LParen, "(")); pos++; continue; }
                if (c == ')') { tokens.Add(new Token(TokenType.RParen, ")")); pos++; continue; }

                // Operators
                if (c == '>' && pos + 1 < span.Length && span[pos + 1] == '=')
                { tokens.Add(new Token(TokenType.Operator, ">=")); pos += 2; continue; }
                if (c == '<' && pos + 1 < span.Length && span[pos + 1] == '=')
                { tokens.Add(new Token(TokenType.Operator, "<=")); pos += 2; continue; }
                if (c == '!' && pos + 1 < span.Length && span[pos + 1] == '=')
                { tokens.Add(new Token(TokenType.Operator, "!=")); pos += 2; continue; }
                if (c == '=' && pos + 1 < span.Length && span[pos + 1] == '=')
                { tokens.Add(new Token(TokenType.Operator, "==")); pos += 2; continue; }
                if (c == '>') { tokens.Add(new Token(TokenType.Operator, ">")); pos++; continue; }
                if (c == '<') { tokens.Add(new Token(TokenType.Operator, "<")); pos++; continue; }

                // String literal
                if (c == '\'' || c == '"')
                {
                    char quote = c;
                    pos++;
                    int start = pos;
                    while (pos < span.Length && span[pos] != quote)
                        pos++;
                    var str = span[start..pos].ToString();
                    if (pos < span.Length) pos++; // skip closing quote
                    tokens.Add(new Token(TokenType.String, str));
                    continue;
                }

                // Number
                if (char.IsDigit(c) || (c == '-' && pos + 1 < span.Length && char.IsDigit(span[pos + 1])))
                {
                    int start = pos;
                    if (c == '-') pos++;
                    while (pos < span.Length && (char.IsDigit(span[pos]) || span[pos] == '.'))
                        pos++;
                    tokens.Add(new Token(TokenType.Number, span[start..pos].ToString()));
                    continue;
                }

                // Identifier / keyword
                if (char.IsLetter(c) || c == '_')
                {
                    int start = pos;
                    while (pos < span.Length && (char.IsLetterOrDigit(span[pos]) || span[pos] == '_'))
                        pos++;
                    var word = span[start..pos].ToString();

                    var upper = word.ToUpperInvariant();
                    if (upper == "AND") { tokens.Add(new Token(TokenType.And, "AND")); continue; }
                    if (upper == "OR") { tokens.Add(new Token(TokenType.Or, "OR")); continue; }
                    if (upper == "NOT") { tokens.Add(new Token(TokenType.Not, "NOT")); continue; }
                    if (upper == "BETWEEN") { tokens.Add(new Token(TokenType.Between, "BETWEEN")); continue; }
                    if (upper == "IN") { tokens.Add(new Token(TokenType.In, "IN")); continue; }
                    if (upper == "CONTAINS") { tokens.Add(new Token(TokenType.Contains, "CONTAINS")); continue; }
                    if (upper == "STARTS" && pos < span.Length)
                    {
                        // "starts with" — peek ahead
                        int saved = pos;
                        while (pos < span.Length && char.IsWhiteSpace(span[pos])) pos++;
                        int wStart = pos;
                        while (pos < span.Length && char.IsLetter(span[pos])) pos++;
                        var next = span[wStart..pos].ToString();
                        if (next.ToUpperInvariant() == "WITH")
                        {
                            tokens.Add(new Token(TokenType.StartsWith, "STARTS_WITH"));
                            continue;
                        }
                        // Not "starts with", revert
                        pos = saved;
                        tokens.Add(new Token(TokenType.Identifier, word));
                        continue;
                    }
                    if (upper == "ENDS" && pos < span.Length)
                    {
                        int saved = pos;
                        while (pos < span.Length && char.IsWhiteSpace(span[pos])) pos++;
                        int wStart = pos;
                        while (pos < span.Length && char.IsLetter(span[pos])) pos++;
                        var next = span[wStart..pos].ToString();
                        if (next.ToUpperInvariant() == "WITH")
                        {
                            tokens.Add(new Token(TokenType.EndsWith, "ENDS_WITH"));
                            continue;
                        }
                        pos = saved;
                        tokens.Add(new Token(TokenType.Identifier, word));
                        continue;
                    }
                    if (upper == "IS" && pos < span.Length)
                    {
                        int saved = pos;
                        while (pos < span.Length && char.IsWhiteSpace(span[pos])) pos++;
                        int wStart = pos;
                        while (pos < span.Length && char.IsLetter(span[pos])) pos++;
                        var next = span[wStart..pos].ToString();
                        if (next.ToUpperInvariant() == "NOT")
                        {
                            // "is not null"
                            while (pos < span.Length && char.IsWhiteSpace(span[pos])) pos++;
                            int nStart = pos;
                            while (pos < span.Length && char.IsLetter(span[pos])) pos++;
                            var nullWord = span[nStart..pos].ToString();
                            if (nullWord.ToUpperInvariant() == "NULL")
                            {
                                tokens.Add(new Token(TokenType.IsNotNull, "IS_NOT_NULL"));
                                continue;
                            }
                        }
                        if (next.ToUpperInvariant() == "NULL")
                        {
                            tokens.Add(new Token(TokenType.IsNull, "IS_NULL"));
                            continue;
                        }
                        pos = saved;
                        tokens.Add(new Token(TokenType.Identifier, word));
                        continue;
                    }

                    tokens.Add(new Token(TokenType.Identifier, word));
                    continue;
                }

                // Skip unknown characters
                pos++;
            }

            tokens.Add(new Token(TokenType.Eof, ""));
            return tokens;
        }

        #endregion

        #region AST

        private abstract class AstNode
        {
            public abstract Func<Dictionary<string, object>, bool> ToPredicate();
            public abstract ITabularQuery ApplyTo(ITabularQuery query);
        }

        private class ComparisonNode : AstNode
        {
            public string Column { get; }
            public string Op { get; }
            public string Value { get; }

            public ComparisonNode(string column, string op, string value)
            {
                Column = column;
                Op = op;
                Value = value;
            }

            public override Func<Dictionary<string, object>, bool> ToPredicate()
            {
                return row =>
                {
                    if (!row.TryGetValue(Column, out var cellVal) || cellVal == null)
                        return false;

                    // Try numeric comparison
                    if (double.TryParse(Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var numVal))
                    {
                        var cellNum = Convert.ToDouble(cellVal);
                        return Op switch
                        {
                            ">" => cellNum > numVal,
                            ">=" => cellNum >= numVal,
                            "<" => cellNum < numVal,
                            "<=" => cellNum <= numVal,
                            "==" => cellNum == numVal,
                            "!=" => cellNum != numVal,
                            _ => false
                        };
                    }

                    // String comparison
                    var cellStr = cellVal?.ToString() ?? "";
                    return Op switch
                    {
                        "==" => cellStr == Value,
                        "!=" => cellStr != Value,
                        ">" => string.Compare(cellStr, Value, StringComparison.Ordinal) > 0,
                        ">=" => string.Compare(cellStr, Value, StringComparison.Ordinal) >= 0,
                        "<" => string.Compare(cellStr, Value, StringComparison.Ordinal) < 0,
                        "<=" => string.Compare(cellStr, Value, StringComparison.Ordinal) <= 0,
                        _ => false
                    };
                };
            }

            public override ITabularQuery ApplyTo(ITabularQuery query)
            {
                var op = Op switch
                {
                    "==" => QueryOp.Eq,
                    "!=" => QueryOp.Ne,
                    ">" => QueryOp.Gt,
                    ">=" => QueryOp.Ge,
                    "<" => QueryOp.Lt,
                    "<=" => QueryOp.Le,
                    _ => QueryOp.Eq
                };

                object val = double.TryParse(Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var numVal)
                    ? numVal
                    : (object)Value;

                return query.Where(Column, op, val);
            }
        }

        private class ContainsNode : AstNode
        {
            public string Column { get; }
            public string Value { get; }

            public ContainsNode(string column, string value) { Column = column; Value = value; }

            public override Func<Dictionary<string, object>, bool> ToPredicate()
            {
                return row => row.TryGetValue(Column, out var v) && v?.ToString().Contains(Value) == true;
            }

            public override ITabularQuery ApplyTo(ITabularQuery query) => query.WhereContains(Column, Value);
        }

        private class StartsWithNode : AstNode
        {
            public string Column { get; }
            public string Value { get; }

            public StartsWithNode(string column, string value) { Column = column; Value = value; }

            public override Func<Dictionary<string, object>, bool> ToPredicate()
            {
                return row => row.TryGetValue(Column, out var v) && v?.ToString().StartsWith(Value) == true;
            }

            public override ITabularQuery ApplyTo(ITabularQuery query) => query.WhereStartsWith(Column, Value);
        }

        private class EndsWithNode : AstNode
        {
            public string Column { get; }
            public string Value { get; }

            public EndsWithNode(string column, string value) { Column = column; Value = value; }

            public override Func<Dictionary<string, object>, bool> ToPredicate()
            {
                return row => row.TryGetValue(Column, out var v) && v?.ToString().EndsWith(Value) == true;
            }

            public override ITabularQuery ApplyTo(ITabularQuery query)
            {
                // ITabularQuery doesn't have WhereEndsWith, use WhereContains + lambda filter
                // For now, wrap with a custom filter
                return query.WhereCustomFilter(dict =>
                    dict.TryGetValue(Column, out var v) && v?.ToString().EndsWith(Value) == true);
            }
        }

        private class IsNullNode : AstNode
        {
            public string Column { get; }
            public bool Negate { get; }

            public IsNullNode(string column, bool negate) { Column = column; Negate = negate; }

            public override Func<Dictionary<string, object>, bool> ToPredicate()
            {
                if (Negate)
                    return row => row.TryGetValue(Column, out var v) && v != null;
                return row => !row.TryGetValue(Column, out var v) || v == null;
            }

            public override ITabularQuery ApplyTo(ITabularQuery query)
            {
                return Negate ? query.WhereIsNotNull(Column) : query.WhereIsNull(Column);
            }
        }

        private class BetweenNode : AstNode
        {
            public string Column { get; }
            public double Min { get; }
            public double Max { get; }

            public BetweenNode(string column, double min, double max)
            { Column = column; Min = min; Max = max; }

            public override Func<Dictionary<string, object>, bool> ToPredicate()
            {
                return row =>
                {
                    if (!row.TryGetValue(Column, out var v) || v == null) return false;
                    var val = Convert.ToDouble(v);
                    return val >= Min && val <= Max;
                };
            }

            public override ITabularQuery ApplyTo(ITabularQuery query) => query.WhereBetween(Column, Min, Max);
        }

        private class InNode : AstNode
        {
            public string Column { get; }
            public List<string> Values { get; }

            public InNode(string column, List<string> values) { Column = column; Values = values; }

            public override Func<Dictionary<string, object>, bool> ToPredicate()
            {
                var set = new HashSet<string>(Values, StringComparer.OrdinalIgnoreCase);
                return row =>
                {
                    if (!row.TryGetValue(Column, out var v) || v == null) return false;
                    return set.Contains(v.ToString());
                };
            }

            public override ITabularQuery ApplyTo(ITabularQuery query)
            {
                // Try numeric IN first
                var nums = new List<double>();
                bool allNumeric = true;
                foreach (var v in Values)
                {
                    if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
                        nums.Add(n);
                    else { allNumeric = false; break; }
                }

                if (allNumeric)
                    return query.WhereIn(Column, nums);
                return query.WhereIn(Column, Values);
            }
        }

        private class AndNode : AstNode
        {
            public AstNode Left { get; }
            public AstNode Right { get; }

            public AndNode(AstNode left, AstNode right) { Left = left; Right = right; }

            public override Func<Dictionary<string, object>, bool> ToPredicate()
            {
                var l = Left.ToPredicate();
                var r = Right.ToPredicate();
                return row => l(row) && r(row);
            }

            public override ITabularQuery ApplyTo(ITabularQuery query)
            {
                // Apply both filters to the same query (they chain)
                return Right.ApplyTo(Left.ApplyTo(query));
            }
        }

        private class OrNode : AstNode
        {
            public AstNode Left { get; }
            public AstNode Right { get; }

            public OrNode(AstNode left, AstNode right) { Left = left; Right = right; }

            public override Func<Dictionary<string, object>, bool> ToPredicate()
            {
                var l = Left.ToPredicate();
                var r = Right.ToPredicate();
                return row => l(row) || r(row);
            }

            public override ITabularQuery ApplyTo(ITabularQuery query)
            {
                // OR can't be easily chained on ITabularQuery (it's AND-based).
                // Fall back to lambda filter.
                var l = Left.ToPredicate();
                var r = Right.ToPredicate();
                return query.WhereCustomFilter(dict => l(dict) || r(dict));
            }
        }

        private class NotNode : AstNode
        {
            public AstNode Child { get; }

            public NotNode(AstNode child) { Child = child; }

            public override Func<Dictionary<string, object>, bool> ToPredicate()
            {
                var c = Child.ToPredicate();
                return row => !c(row);
            }

            public override ITabularQuery ApplyTo(ITabularQuery query)
            {
                // NOT can't be directly expressed on ITabularQuery. Use lambda.
                var c = Child.ToPredicate();
                return query.WhereCustomFilter(dict => !c(dict));
            }
        }

        #endregion

        #region Parser (Recursive Descent)

        private class Parser
        {
            private readonly List<Token> _tokens;
            private int _pos;

            public Parser(List<Token> tokens) { _tokens = tokens; _pos = 0; }

            private Token Peek => _tokens[_pos];
            private Token Advance() { var t = _tokens[_pos]; _pos++; return t; }

            private bool Match(TokenType type)
            {
                if (Peek.Type == type) { _pos++; return true; }
                return false;
            }

            // expression = or_expr
            public AstNode ParseExpression() => ParseOr();

            // or_expr = and_expr (OR and_expr)*
            private AstNode ParseOr()
            {
                var left = ParseAnd();
                while (Peek.Type == TokenType.Or)
                {
                    Advance();
                    var right = ParseAnd();
                    left = new OrNode(left, right);
                }
                return left;
            }

            // and_expr = not_expr (AND not_expr)*
            private AstNode ParseAnd()
            {
                var left = ParseNot();
                while (Peek.Type == TokenType.And)
                {
                    Advance();
                    var right = ParseNot();
                    left = new AndNode(left, right);
                }
                return left;
            }

            // not_expr = NOT not_expr | primary
            private AstNode ParseNot()
            {
                if (Peek.Type == TokenType.Not)
                {
                    Advance();
                    var child = ParseNot();
                    return new NotNode(child);
                }
                return ParsePrimary();
            }

            // primary = "(" expression ")" | condition
            private AstNode ParsePrimary()
            {
                if (Peek.Type == TokenType.LParen)
                {
                    Advance();
                    var expr = ParseExpression();
                    if (Peek.Type == TokenType.RParen) Advance();
                    return expr;
                }
                return ParseCondition();
            }

            // condition = column op value
            //           | column CONTAINS value
            //           | column STARTS_WITH value
            //           | column ENDS_WITH value
            //           | column IS_NULL
            //           | column IS_NOT_NULL
            //           | column BETWEEN value value
            //           | column IN value+
            private AstNode ParseCondition()
            {
                // Expect identifier (column name)
                if (Peek.Type != TokenType.Identifier)
                    throw new InvalidOperationException($"Expected column name, got {Peek.Type}: '{Peek.Value}'");

                var column = Advance().Value;

                // IS NULL / IS NOT NULL
                if (Peek.Type == TokenType.IsNull)
                {
                    Advance();
                    return new IsNullNode(column, false);
                }
                if (Peek.Type == TokenType.IsNotNull)
                {
                    Advance();
                    return new IsNullNode(column, true);
                }

                // CONTAINS
                if (Peek.Type == TokenType.Contains)
                {
                    Advance();
                    var val = ExpectValue();
                    return new ContainsNode(column, val);
                }

                // STARTS WITH
                if (Peek.Type == TokenType.StartsWith)
                {
                    Advance();
                    var val = ExpectValue();
                    return new StartsWithNode(column, val);
                }

                // ENDS WITH
                if (Peek.Type == TokenType.EndsWith)
                {
                    Advance();
                    var val = ExpectValue();
                    return new EndsWithNode(column, val);
                }

                // BETWEEN
                if (Peek.Type == TokenType.Between)
                {
                    Advance();
                    var minStr = ExpectValue();
                    var maxStr = ExpectValue();
                    if (!double.TryParse(minStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var min))
                        throw new InvalidOperationException($"BETWEEN min must be numeric, got '{minStr}'");
                    if (!double.TryParse(maxStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var max))
                        throw new InvalidOperationException($"BETWEEN max must be numeric, got '{maxStr}'");
                    return new BetweenNode(column, min, max);
                }

                // IN
                if (Peek.Type == TokenType.In)
                {
                    Advance();
                    var values = new List<string>();
                    while (Peek.Type == TokenType.String || Peek.Type == TokenType.Number || Peek.Type == TokenType.Identifier)
                    {
                        values.Add(Advance().Value);
                    }
                    if (values.Count == 0)
                        throw new InvalidOperationException("IN requires at least one value");
                    return new InNode(column, values);
                }

                // Comparison operator
                if (Peek.Type == TokenType.Operator)
                {
                    var op = Advance().Value;
                    var val = ExpectValue();
                    return new ComparisonNode(column, op, val);
                }

                throw new InvalidOperationException($"Expected operator or keyword after column '{column}', got {Peek.Type}: '{Peek.Value}'");
            }

            private string ExpectValue()
            {
                return Peek.Type switch
                {
                    TokenType.String => Advance().Value,
                    TokenType.Number => Advance().Value,
                    TokenType.Identifier => Advance().Value,
                    _ => throw new InvalidOperationException($"Expected value, got {Peek.Type}: '{Peek.Value}'")
                };
            }
        }

        #endregion
    }

    // Extension to allow LambdaFilteredQuery-style wrapping from parser
    internal static class QueryCustomExtension
    {
        public static ITabularQuery WhereCustomFilter(this ITabularQuery query, Func<Dictionary<string, object>, bool> predicate)
        {
            return TabularQueryExtensions.WhereCustom(query, predicate);
        }
    }
}
