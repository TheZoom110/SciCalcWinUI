using System;
using System.Collections.Generic;

namespace SDC_B_3_SciCalcWinUI
{
    public static class ExpressionParser
    {
        private static string expr;
        private static int pos;

        public static double Evaluate(string expression)
        {
            string e = expression.Replace(" ", "");

            // Insert * before pi and e when preceded by a digit
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < e.Length; i++)
            {
                // Check for 'pi'
                if (i + 2 <= e.Length && e.Substring(i, 2) == "pi")
                {
                    if (i > 0 && (char.IsDigit(e[i - 1]) || e[i - 1] == ')'))
                        sb.Append('*');
                    sb.Append(Math.PI.ToString("G15"));
                    i += 1; // skip 'i' of 'pi'
                }
                // Check for 'e'
                else if (e[i] == 'e')
                {
                    if (i > 0 && (char.IsDigit(e[i - 1]) || e[i - 1] == ')'))
                        sb.Append('*');
                    sb.Append(Math.E.ToString("G15"));
                }
                else
                {
                    sb.Append(e[i]);
                }
            }

            expr = sb.ToString();
            pos = 0;
            double result = ParseAddSubtract();
            return result;
        }

        // Handles + and -
        private static double ParseAddSubtract()
        {
            double left = ParseMultiplyDivide();
            while (pos < expr.Length && (expr[pos] == '+' || expr[pos] == '-'))
            {
                char op = expr[pos++];
                double right = ParseMultiplyDivide();
                left = op == '+' ? left + right : left - right;
            }
            return left;
        }

        // Handles * and /
        private static double ParseMultiplyDivide()
        {
            double left = ParsePower();
            while (pos < expr.Length)
            {
                if (expr[pos] == '*' || expr[pos] == '/')
                {
                    char op = expr[pos++];
                    double right = ParsePower();
                    if (op == '/' && right == 0) return double.NaN;
                    left = op == '*' ? left * right : left / right;
                }
                else if (expr.Substring(pos).StartsWith("mod"))
                {
                    pos += 3; // skip 'mod'
                    double right = ParsePower();
                    left = right != 0 ? left % right : double.NaN;
                }
                // Implicit multiplication: number followed by '(', a function, or a constant
                else if (expr[pos] == '(' ||
                         expr.Substring(pos).StartsWith("sin") ||
                         expr.Substring(pos).StartsWith("cos") ||
                         expr.Substring(pos).StartsWith("tan") ||
                         expr.Substring(pos).StartsWith("log") ||
                         expr.Substring(pos).StartsWith("ln") ||
                         expr.Substring(pos).StartsWith("mod") ||
                         expr.Substring(pos).StartsWith(Math.PI.ToString("G15")) ||
                         expr.Substring(pos).StartsWith(Math.E.ToString("G15")))
                {
                    double right = ParsePower();
                    left = left * right;
                }
                else break;
            }
            return left;
        }

        // Handles ^
        private static double ParsePower()
        {
            double left = ParseUnary();
            if (pos < expr.Length && expr[pos] == '^')
            {
                pos++;
                double right = ParseUnary();
                left = Math.Pow(left, right);
            }
            return left;
        }

        // Handles unary minus and functions
        private static double ParseUnary()
        {
            if (pos < expr.Length && expr[pos] == '-')
            {
                pos++;
                return -ParseUnary();
            }
            return ParsePrimary();
        }

        // Handles numbers, parentheses, and functions
        private static double ParsePrimary()
        {
            // Parentheses
            if (pos < expr.Length && expr[pos] == '(')
            {
                pos++; // skip '('
                double result = ParseAddSubtract();
                if (pos < expr.Length && expr[pos] == ')') pos++; // skip ')'
                return result;
            }

            // Functions
            string[] functions = { "sin", "cos", "tan", "log", "ln", "sqrt", "mod" };
            foreach (string fn in functions)
            {
                if (pos + fn.Length <= expr.Length &&
                    expr.Substring(pos, fn.Length) == fn)
                {
                    pos += fn.Length;
                    double arg = ParsePrimary();
                    switch (fn)
                    {
                        case "sin": return RoundTrig(Math.Sin(arg * Math.PI / 180.0));
                        case "cos": return RoundTrig(Math.Cos(arg * Math.PI / 180.0));
                        case "tan": return TanDeg(arg);
                        case "log": return Math.Log10(arg);
                        case "ln": return Math.Log(arg);
                        case "sqrt": return arg >= 0 ? Math.Sqrt(arg) : double.NaN;
                        case "mod": return double.NaN; // mod is handled as binary op, not unary
                        default: return double.NaN;
                    }
                }
            }

            // Numbers (including scientific notation e.g. 1.5E+33, 2.3E-10)
            int start = pos;
            if (pos < expr.Length && expr[pos] == '-') pos++;
            while (pos < expr.Length && (char.IsDigit(expr[pos]) || expr[pos] == '.'))
                pos++;

            // Handle scientific notation: E or e followed by optional + or - and digits
            if (pos < expr.Length && (expr[pos] == 'E' || expr[pos] == 'e'))
            {
                pos++; // consume E
                if (pos < expr.Length && (expr[pos] == '+' || expr[pos] == '-'))
                    pos++; // consume + or -
                while (pos < expr.Length && char.IsDigit(expr[pos]))
                    pos++; // consume exponent digits
            }

            if (pos == start) return double.NaN;
            return double.Parse(expr.Substring(start, pos - start),
                System.Globalization.CultureInfo.InvariantCulture);
        }

        // ── Trig Helpers ──────────────────────────────────────────

        private static double RoundTrig(double value)
        {
            return Math.Round(value, 10);
        }

        private static double TanDeg(double degrees)
        {
            // Normalize to 0-360
            double d = degrees % 360;
            if (d < 0) d += 360;

            // Special case angles where tan is undefined or exactly zero
            if (d == 90 || d == 270) return double.NaN; // undefined → Error
            if (d == 0 || d == 180) return 0;

            return RoundTrig(Math.Tan(d * Math.PI / 180.0));
        }
    }
}