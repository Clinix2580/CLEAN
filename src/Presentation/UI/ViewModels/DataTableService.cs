using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace OS.Presentation.UI.ViewModels;

/// <summary>
/// Columna de tabla de datos.
/// </summary>
public class DataTableColumn
{
    public string PropertyName { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public bool IsSortable { get; set; }
    public bool IsFilterable { get; set; }
    public string? Format { get; set; }
}

/// <summary>
/// Configuración de paginación.
/// </summary>
public class PaginationConfig
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}

/// <summary>
/// Estado de selección múltiple.
/// </summary>
public class MultiSelectionState
{
    public HashSet<Guid> SelectedIds { get; } = new();
    public bool IsAllSelected { get; set; }
    public int SelectedCount => SelectedIds.Count;
    public bool HasSelection => SelectedCount > 0;
}

/// <summary>
/// Servicio de tabla de datos.
/// </summary>
public interface IDataTableService
{
    /// <summary>
    /// Ordena los datos por una columna.
    /// </summary>
    Task<List<T>> SortAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        List<T> data,
        string propertyName,
        bool ascending);

    /// <summary>
    /// Filtra los datos por término de búsqueda.
    /// </summary>
    Task<List<T>> FilterAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        List<T> data,
        string searchTerm);

    /// <summary>
    /// Aplica paginación a los datos.
    /// </summary>
    Task<List<T>> PaginateAsync<T>(List<T> data, PaginationConfig config);

    /// <summary>
    /// Exporta los datos a CSV.
    /// </summary>
    Task<byte[]> ExportToCsvAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        List<T> data,
        List<DataTableColumn> columns);
}

/// <summary>
/// Implementación del servicio de tabla de datos.
/// </summary>
public class DataTableService : IDataTableService
{
    /// <summary>
    /// Ordena los datos por una columna.
    /// </summary>
    public async Task<List<T>> SortAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        List<T> data,
        string propertyName,
        bool ascending)
    {
        return await Task.Run(() =>
        {
            var property = typeof(T).GetProperty(propertyName);
            if (property == null)
                return data;

            var sortedData = ascending
                ? data.OrderBy(x => property.GetValue(x)).ToList()
                : data.OrderByDescending(x => property.GetValue(x)).ToList();

            return sortedData;
        });
    }

    /// <summary>
    /// Filtra los datos por término de búsqueda.
    /// </summary>
    public async Task<List<T>> FilterAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        List<T> data,
        string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return data;

        return await Task.Run(() =>
        {
            var lowerTerm = searchTerm.ToLowerInvariant();
            var filteredData = new List<T>();

            foreach (var item in data)
            {
                var properties = typeof(T).GetProperties();
                foreach (var prop in properties)
                {
                    var value = prop.GetValue(item)?.ToString();
                    if (value != null && value.ToLowerInvariant().Contains(lowerTerm))
                    {
                        filteredData.Add(item);
                        break;
                    }
                }
            }

            return filteredData;
        });
    }

    /// <summary>
    /// Aplica paginación a los datos.
    /// </summary>
    public async Task<List<T>> PaginateAsync<T>(List<T> data, PaginationConfig config)
    {
        return await Task.Run(() =>
        {
            var skip = (config.PageNumber - 1) * config.PageSize;
            return data.Skip(skip).Take(config.PageSize).ToList();
        });
    }

    /// <summary>
    /// Exporta los datos a CSV.
    /// </summary>
    public async Task<byte[]> ExportToCsvAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        List<T> data,
        List<DataTableColumn> columns)
    {
        return await Task.Run(() =>
        {
            var csv = new System.Text.StringBuilder();

            // Header
            var headers = string.Join(",", columns.Select(c => c.Header));
            csv.AppendLine(headers);

            // Rows
            foreach (var item in data)
            {
                var values = new List<string>();
                foreach (var column in columns)
                {
                    var property = typeof(T).GetProperty(column.PropertyName);
                    var value = property?.GetValue(item)?.ToString() ?? string.Empty;
                    values.Add($"\"{value}\""); // Escape quotes
                }
                csv.AppendLine(string.Join(",", values));
            }

            return System.Text.Encoding.UTF8.GetBytes(csv.ToString());
        });
    }
}
