using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using OS.Infrastructure.Data;
using OS.Domain.Entities;

namespace OS.Presentation.UI.ViewModels;

/// <summary>
/// ViewModel para la timeline de auditoría con filtros y buscador.
/// </summary>
public class AuditTimelineViewModel : INotifyPropertyChanged
{
    private readonly ClinicDbContext _dbContext;

    private List<AuditLog> _allAuditLogs = new();
    private List<AuditLog> _filteredAuditLogs = new();
    private string _searchText = string.Empty;
    private string _selectedEntityType = string.Empty;
    private string _selectedAction = string.Empty;
    private DateTime? _startDate;
    private DateTime? _endDate;
    private bool _isLoading;

    public AuditTimelineViewModel(ClinicDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Logs de auditoría filtrados.
    /// </summary>
    public List<AuditLog> FilteredAuditLogs
    {
        get => _filteredAuditLogs;
        private set
        {
            if (_filteredAuditLogs != value)
            {
                _filteredAuditLogs = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalCount));
            }
        }
    }

    /// <summary>
    /// Texto de búsqueda.
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }
    }

    /// <summary>
    /// Tipo de entidad seleccionado para filtro.
    /// </summary>
    public string SelectedEntityType
    {
        get => _selectedEntityType;
        set
        {
            if (_selectedEntityType != value)
            {
                _selectedEntityType = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }
    }

    /// <summary>
    /// Acción seleccionada para filtro.
    /// </summary>
    public string SelectedAction
    {
        get => _selectedAction;
        set
        {
            if (_selectedAction != value)
            {
                _selectedAction = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }
    }

    /// <summary>
    /// Fecha de inicio para filtro de rango.
    /// </summary>
    public DateTime? StartDate
    {
        get => _startDate;
        set
        {
            if (_startDate != value)
            {
                _startDate = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }
    }

    /// <summary>
    /// Fecha de fin para filtro de rango.
    /// </summary>
    public DateTime? EndDate
    {
        get => _endDate;
        set
        {
            if (_endDate != value)
            {
                _endDate = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }
    }

    /// <summary>
    /// Indica si se está cargando información.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Total de registros filtrados.
    /// </summary>
    public int TotalCount => FilteredAuditLogs.Count;

    /// <summary>
    /// Lista de tipos de entidad disponibles para filtro.
    /// </summary>
    public List<string> AvailableEntityTypes { get; private set; } = new();

    /// <summary>
    /// Lista de acciones disponibles para filtro.
    /// </summary>
    public List<string> AvailableActions { get; private set; } = new();

    /// <summary>
    /// Carga los logs de auditoría.
    /// </summary>
    public async Task LoadAuditLogsAsync()
    {
        IsLoading = true;

        try
        {
            _allAuditLogs = await _dbContext.AuditLogs
                .OrderByDescending(a => a.CreatedAt)
                .Take(1000) // Limitar a 1000 registros para rendimiento
                .ToListAsync();

            // Extraer tipos de entidad y acciones únicos
            AvailableEntityTypes = _allAuditLogs
                .Select(a => a.EntityType)
                .Distinct()
                .OrderBy(e => e)
                .ToList();

            AvailableActions = _allAuditLogs
                .Select(a => a.Action)
                .Distinct()
                .OrderBy(a => a)
                .ToList();

            ApplyFilters();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Aplica los filtros actuales.
    /// </summary>
    [SuppressMessage("IL", "IL3050")]
    [SuppressMessage("IL", "IL2026")]
    private void ApplyFilters()
    {
        var query = _allAuditLogs.AsQueryable();

        // Filtro de búsqueda
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLowerInvariant();
            query = query.Where(a =>
                a.EntityType.ToLowerInvariant().Contains(searchLower) ||
                a.Action.ToLowerInvariant().Contains(searchLower) ||
                (a.EntityId != null && a.EntityId.ToLowerInvariant().Contains(searchLower)));
        }

        // Filtro de tipo de entidad
        if (!string.IsNullOrWhiteSpace(SelectedEntityType))
        {
            query = query.Where(a => a.EntityType == SelectedEntityType);
        }

        // Filtro de acción
        if (!string.IsNullOrWhiteSpace(SelectedAction))
        {
            query = query.Where(a => a.Action == SelectedAction);
        }

        // Filtro de rango de fechas
        if (StartDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= StartDate.Value.Date);
        }

        if (EndDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= EndDate.Value.Date.AddDays(1).AddTicks(-1));
        }

        FilteredAuditLogs = query.OrderByDescending(a => a.CreatedAt).ToList();
    }

    /// <summary>
    /// Limpia todos los filtros.
    /// </summary>
    public void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedEntityType = string.Empty;
        SelectedAction = string.Empty;
        StartDate = null;
        EndDate = null;
    }

    /// <summary>
    /// Exporta los logs filtrados a CSV.
    /// </summary>
    public string ExportToCsv()
    {
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("ID,EntityType,Action,EntityId,UserId,CreatedAt,Hash");

        foreach (var log in FilteredAuditLogs)
        {
            csv.AppendLine($"{log.Id},{log.EntityType},{log.Action},{log.EntityId},{log.CreatedBy},{log.CreatedAt:O},{log.Hash}");
        }

        return csv.ToString();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
