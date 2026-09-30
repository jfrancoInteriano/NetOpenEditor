using System.Linq.Expressions;
using System.Reflection;

namespace NetOpenEditor.Options;

internal static class MemberSelector
{
    /// <summary>Extracts the member name and compiles the selector once. Only direct member access (l => l.Prop) is allowed.</summary>
    public static (string Field, Func<TLine, object?> Getter) Compile<TLine, TProp>(Expression<Func<TLine, TProp>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        var body = selector.Body is UnaryExpression { NodeType: ExpressionType.Convert } unary ? unary.Operand : selector.Body;
        if (body is not MemberExpression { Member: PropertyInfo or FieldInfo } member || member.Expression != selector.Parameters[0])
        {
            throw new EditorConfigurationException(
                $"Column selector '{selector}' must be a direct member access such as l => l.Property.");
        }

        var compiled = selector.Compile();
        return (member.Member.Name, line => compiled(line));
    }
}
