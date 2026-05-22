using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using OS.Application.Interfaces;
using OS.Domain.Entities;
using OS.Domain.Enums;
using OS.Presentation;

namespace OS.Presentation.ViewModels;

/// <summary>
/// ViewModel para el Dashboard de Cumplimiento Normativo.
/// Usa métricas reales de auditoría e implementación de normas en lugar de datos simulados.
/// </summary>
public class ComplianceDashboardViewModel : INotifyPropertyChanged
{
    private const int RecentAuditLogLimit = 20;
    private readonly IComplianceManager _complianceManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DispatcherTimer _updateTimer;

    private double _hipaaComplianceScore;
    private string _hipaaStatus = string.Empty;
    private Brush _hipaaStatusColor = Brushes.Gray;
    private string _hipaaLastCheck = string.Empty;
    private ObservableCollection<ComplianceAlert> _hipaaAlerts = [];

    private double _lfpdpppComplianceScore;
    private string _lfpdpppStatus = string.Empty;
    private Brush _lfpdpppStatusColor = Brushes.Gray;
    private string _lfpdpppLastCheck = string.Empty;
    private ObservableCollection<ComplianceAlert> _lfpdpppAlerts = [];

    private string _overallStatus = "Inicializando...";
    private Brush _overallStatusColor = Brushes.Gray;
    private DateTime _lastUpdate;

    public ComplianceDashboardViewModel()
    {
        _complianceManager = App.ServiceProvider.GetRequiredService<IComplianceManager>();
        _scopeFactory = App.ServiceProvider.GetRequiredService<IServiceScopeFactory>();

        HipaaAlerts = new ObservableCollection<ComplianceAlert>();
        LfpdpppAlerts = new ObservableCollection<ComplianceAlert>();

        InitializeComplianceData();

        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();

        UpdateComplianceData();
    }

    #region Propiedades Públicas

    public double HipaaComplianceScore
    {
        get => _hipaaComplianceScore;
        set
        {
            _hipaaComplianceScore = value;
            OnPropertyChanged();
            UpdateHipaaStatus();
        }
    }

    public string HipaaStatus
    {
        get => _hipaaStatus;
        set
        {
            _hipaaStatus = value;
            OnPropertyChanged();
        }
    }

    public Brush HipaaStatusColor
    {
        get => _hipaaStatusColor;
        set
        {
            _hipaaStatusColor = value;
            OnPropertyChanged();
        }
    }

    public string HipaaLastCheck
    {
        get => _hipaaLastCheck;
        set
        {
            _hipaaLastCheck = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<ComplianceAlert> HipaaAlerts
    {
        get => _hipaaAlerts;
        private set
        {
            _hipaaAlerts = value;
            OnPropertyChanged();
        }
    }

    public double LfpdpppComplianceScore
    {
        get => _lfpdpppComplianceScore;
        set
        {
            _lfpdpppComplianceScore = value;
            OnPropertyChanged();
            UpdateLfpdpppStatus();
        }
    }

    public string LfpdpppStatus
    {
        get => _lfpdpppStatus;
        set
        {
            _lfpdpppStatus = value;
            OnPropertyChanged();
        }
    }

    public Brush LfpdpppStatusColor
    {
        get => _lfpdpppStatusColor;
        set
        {
            _lfpdpppStatusColor = value;
            OnPropertyChanged();
        }
    }

    public string LfpdpppLastCheck
    {
        get => _lfpdpppLastCheck;
        set
        {
            _lfpdpppLastCheck = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<ComplianceAlert> LfpdpppAlerts
    {
        get => _lfpdpppAlerts;
        private set
        {
            _lfpdpppAlerts = value;
            OnPropertyChanged();
        }
    }

    public string OverallStatus
    {
        get => _overallStatus;
        set
        {
            _overallStatus = value;
            OnPropertyChanged();
        }
    }

    public Brush OverallStatusColor
    {
        get => _overallStatusColor;
        set
        {
            _overallStatusColor = value;
            OnPropertyChanged();
        }
    }

    public DateTime LastUpdate
    {
        get => _lastUpdate;
        set
        {
            _lastUpdate = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Métodos Privados

    private void InitializeComplianceData()
    {
        var settings = _complianceManager.Settings;
        if (settings.Region == ComplianceRegion.UsaHipaa)
        {
            HipaaStatus = "HIPAA habilitado";
            HipaaStatusColor = Brushes.Green;
            LfpdpppStatus = "No aplicable en esta región";
            LfpdpppStatusColor = Brushes.Gray;
        }
        else
        {
            LfpdpppStatus = "LFPDPPP habilitado";
            LfpdpppStatusColor = Brushes.Green;
            HipaaStatus = "No aplicable en esta región";
            HipaaStatusColor = Brushes.Gray;
        }

        HipaaLastCheck = "Pendiente";
        LfpdpppLastCheck = "Pendiente";
        OverallStatus = "Esperando datos de auditoría";
        OverallStatusColor = Brushes.Gray;
        LastUpdate = DateTime.Now;
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        UpdateComplianceData();
    }

    private async void UpdateComplianceData()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
            var recentLogs = (await auditService.GetRecentAuditLogsAsync(RecentAuditLogLimit)).ToList();
            var integrityOk = await ValidateAuditChainAsync(auditService, recentLogs);

            UpdateComplianceMetrics(recentLogs, integrityOk);

            LastUpdate = DateTime.Now;
            UpdateOverallStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error actualizando datos de cumplimiento: {ex.Message}",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task<bool> ValidateAuditChainAsync(IAuditService auditService, IEnumerable<AuditLog> logs)
    {
        if (!logs.Any())
            return false;

        foreach (var log in logs)
        {
            if (!await auditService.ValidateAuditLogIntegrityAsync(log))
                return false;
        }

        return true;
    }

    private void UpdateComplianceMetrics(IEnumerable<AuditLog> logs, bool integrityOk)
    {
        var settings = _complianceManager.Settings;
        var hipaaRelevant = settings.Region == ComplianceRegion.UsaHipaa;
        var lfpdpppRelevant = settings.Region == ComplianceRegion.MexicoLfpdppp;

        var hipaaLogs = logs.Where(IsHipaaRelated).Take(10).ToList();
        var lfpdpppLogs = logs.Where(IsLfpdpppRelated).Take(10).ToList();

        HipaaComplianceScore = hipaaRelevant
            ? CalculateComplianceScore(hipaaLogs, integrityOk, settings.AutoLogoutMinutes, settings.AuditEnabled, isHipaa: true)
            : 0;

        LfpdpppComplianceScore = lfpdpppRelevant
            ? CalculateComplianceScore(lfpdpppLogs, integrityOk, settings.AutoLogoutMinutes, settings.ArcoRightsEnabled, isHipaa: false)
            : 0;

        HipaaLastCheck = hipaaRelevant ? GetLastLogTimestamp(hipaaLogs) : "No aplicable";
        LfpdpppLastCheck = lfpdpppRelevant ? GetLastLogTimestamp(lfpdpppLogs) : "No aplicable";

        HipaaAlerts.Clear();
        if (hipaaRelevant)
            AddAlerts(hipaaLogs, HipaaAlerts, "HIPAA");

        LfpdpppAlerts.Clear();
        if (lfpdpppRelevant)
            AddAlerts(lfpdpppLogs, LfpdpppAlerts, "LFPDPPP");
    }

    private static double CalculateComplianceScore(
        IEnumerable<AuditLog> logs,
        bool integrityOk,
        int autoLogoutMinutes,
        bool featureEnabled,
        bool isHipaa)
    {
        var score = 30.0;
        score += featureEnabled ? 30.0 : -10.0;
        score += integrityOk ? 25.0 : -15.0;

        var lastLog = logs.OrderByDescending(log => log.CreatedAt).FirstOrDefault();
        if (lastLog != null)
        {
            var age = DateTime.UtcNow - lastLog.CreatedAt.ToUniversalTime();
            if (age <= TimeSpan.FromHours(24))
                score += 15.0;
            else if (age <= TimeSpan.FromDays(7))
                score += 5.0;
            else
                score -= 10.0;
        }
        else
        {
            score -= 10.0;
        }

        if (isHipaa)
        {
            score += autoLogoutMinutes <= 15 ? 10.0 : autoLogoutMinutes <= 30 ? 5.0 : 0.0;
        }
        else
        {
            score += featureEnabled ? 10.0 : -5.0;
        }

        return Math.Clamp(score, 0.0, 100.0);
    }

    private static bool IsHipaaRelated(AuditLog log)
    {
        if (log == null)
            return false;

        if (log.Action.Contains("ARCO", StringComparison.OrdinalIgnoreCase) ||
            log.EntityType.Contains("Consent", StringComparison.OrdinalIgnoreCase) ||
            log.EntityType.Contains("Arco", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool IsLfpdpppRelated(AuditLog log)
    {
        if (log == null)
            return false;

        if (log.Action.Contains("ARCO", StringComparison.OrdinalIgnoreCase) ||
            log.EntityType.Contains("Consent", StringComparison.OrdinalIgnoreCase) ||
            log.EntityType.Contains("Arco", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string GetLastLogTimestamp(IEnumerable<AuditLog> logs)
    {
        var lastLog = logs.OrderByDescending(log => log.CreatedAt).FirstOrDefault();
        return lastLog == null ? "Sin registros recientes" : lastLog.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private void AddAlerts(IEnumerable<AuditLog> logs, ObservableCollection<ComplianceAlert> target, string regulation)
    {
        if (!logs.Any())
        {
            target.Add(new ComplianceAlert
            {
                Regulation = regulation,
                Severity = AlertSeverity.Info,
                Message = "No se encontraron registros de auditoría recientes para esta regulación.",
                Timestamp = DateTime.Now
            });
            return;
        }

        foreach (var log in logs)
        {
            target.Add(new ComplianceAlert
            {
                Regulation = regulation,
                Severity = MapSeverity(log.Action),
                Message = BuildAlertMessage(log),
                Timestamp = log.CreatedAt
            });
        }
    }

    private static AlertSeverity MapSeverity(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return AlertSeverity.Info;

        if (action.Contains("FAILED", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("DELETE", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("UNAUTHORIZED", StringComparison.OrdinalIgnoreCase))
        {
            return AlertSeverity.Error;
        }

        if (action.Contains("WARNING", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("REQUEST", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("OPENED", StringComparison.OrdinalIgnoreCase))
        {
            return AlertSeverity.Warning;
        }

        return AlertSeverity.Info;
    }

    private static string BuildAlertMessage(AuditLog log)
    {
        return log.EntityId == null
            ? $"{log.Action} en {log.EntityType}."
            : $"{log.Action} en {log.EntityType} (ID: {log.EntityId}).";
    }

    private void UpdateHipaaStatus()
    {
        if (_complianceManager.Settings.Region != ComplianceRegion.UsaHipaa)
        {
            HipaaStatus = "No aplicable en esta región";
            HipaaStatusColor = Brushes.Gray;
            return;
        }

        if (HipaaComplianceScore >= 95)
        {
            HipaaStatus = "Cumplimiento Excelente";
            HipaaStatusColor = Brushes.Green;
        }
        else if (HipaaComplianceScore >= 85)
        {
            HipaaStatus = "Cumplimiento Bueno";
            HipaaStatusColor = Brushes.Yellow;
        }
        else
        {
            HipaaStatus = "Requiere Atención";
            HipaaStatusColor = Brushes.Red;
        }
    }

    private void UpdateLfpdpppStatus()
    {
        if (_complianceManager.Settings.Region != ComplianceRegion.MexicoLfpdppp)
        {
            LfpdpppStatus = "No aplicable en esta región";
            LfpdpppStatusColor = Brushes.Gray;
            return;
        }

        if (LfpdpppComplianceScore >= 95)
        {
            LfpdpppStatus = "Cumplimiento Excelente";
            LfpdpppStatusColor = Brushes.Green;
        }
        else if (LfpdpppComplianceScore >= 85)
        {
            LfpdpppStatus = "Cumplimiento Bueno";
            LfpdpppStatusColor = Brushes.Yellow;
        }
        else
        {
            LfpdpppStatus = "Requiere Atención";
            LfpdpppStatusColor = Brushes.Red;
        }
    }

    private void UpdateOverallStatus()
    {
        var scores = new List<double>();
        if (_complianceManager.Settings.Region == ComplianceRegion.UsaHipaa)
            scores.Add(HipaaComplianceScore);
        if (_complianceManager.Settings.Region == ComplianceRegion.MexicoLfpdppp)
            scores.Add(LfpdpppComplianceScore);

        var avgScore = scores.Any() ? scores.Average() : 0;

        if (avgScore >= 95)
        {
            OverallStatus = "Sistema Cumpliente";
            OverallStatusColor = Brushes.Green;
        }
        else if (avgScore >= 85)
        {
            OverallStatus = "Monitoreo Requerido";
            OverallStatusColor = Brushes.Yellow;
        }
        else
        {
            OverallStatus = "Acción Inmediata Requerida";
            OverallStatusColor = Brushes.Red;
        }
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}

/// <summary>
/// Modelo para alertas de cumplimiento.
/// </summary>
public class ComplianceAlert
{
    public string Regulation { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }

    public Brush SeverityColor => Severity switch
    {
        AlertSeverity.Info => Brushes.Blue,
        AlertSeverity.Warning => Brushes.Orange,
        AlertSeverity.Error => Brushes.Red,
        _ => Brushes.Gray
    };

    public string SeverityText => Severity.ToString();
}

/// <summary>
/// Niveles de severidad para alertas.
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Error
}
