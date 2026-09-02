using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace AppTunnel.Conversores;

internal sealed class ExibirMembroConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not { } item) return null;

        var caminho = values[1] as string;
        if (string.IsNullOrEmpty(caminho)) return item;

        var propriedade = item.GetType().GetProperty(caminho, BindingFlags.Public | BindingFlags.Instance);
        return propriedade?.GetValue(item) ?? item;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
