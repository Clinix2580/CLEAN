using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OS.Infrastructure.Data;
using OS.Domain.Entities;
using OS.Application.Interfaces;
using System.Collections.ObjectModel;

namespace OS.Application.Services;

/// <summary>
/// Resultado de verificación de integridad financiera.
/// </summary>
public class FinancialIntegrityResult
{
    public bool IsIntegrityValid { get; set; }
    public int TotalRecords { get; set; }
    public int ValidRecords { get; set; }
    public int InvalidRecords { get; set; }
    public Collection<string> Violations { get; } = new();
}

/// <summary>
/// Servicio de finanzas con blindaje criptográfico de transacciones.
/// </summary>
public interface IFinancialService
{
    /// <summary>
    /// Crea un registro de pago con hash de cadena.
    /// </summary>
    Task<PaymentRecord> CreatePaymentRecordAsync(PaymentRecord record, Guid cashierId);

    /// <summary>
    /// Anula un registro de pago.
    /// </summary>
    Task<bool> VoidPaymentRecordAsync(Guid paymentId, Guid voidedBy, string reason);

    /// <summary>
    /// Verifica la integridad de la cadena de transacciones.
    /// </summary>
    Task<FinancialIntegrityResult> VerifyFinancialIntegrityAsync();

    /// <summary>
    /// Genera el siguiente folio financiero.
    /// </summary>
    Task<string> GenerateNextFolioAsync();

    /// <summary>
    /// Abre una sesión de caja.
    /// </summary>
    Task<CashRegisterSession> OpenCashRegisterSessionAsync(Guid cashierId, decimal openingAmount);

    /// <summary>
    /// Cierra una sesión de caja (cierre ciego).
    /// </summary>
    Task<CashRegisterSession> CloseCashRegisterSessionAsync(Guid sessionId, decimal declaredAmount);

    /// <summary>
    /// Obtiene la sesión de caja activa.
    /// </summary>
    Task<CashRegisterSession?> GetActiveSessionAsync(Guid cashierId);

    /// <summary>
    /// Exporta pólizas a CSV.
    /// </summary>
    Task<string> ExportPoliciesToCsvAsync(DateTime startDate, DateTime endDate);
}

/// <summary>
/// Implementación del servicio de finanzas.
/// </summary>
public class FinancialService : IFinancialService
{
    private readonly ClinicDbContext _dbContext;
    private readonly IAuditService _auditService;

    public FinancialService(
        ClinicDbContext dbContext,
        IAuditService auditService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    /// <summary>
    /// Crea un registro de pago con hash de cadena.
    /// </summary>
    public async Task<PaymentRecord> CreatePaymentRecordAsync(PaymentRecord record, Guid cashierId)
    {
        // Obtener el hash del registro anterior
        var previousRecord = await _dbContext.PaymentRecords
            .OrderByDescending(p => p.PaymentDate)
            .FirstOrDefaultAsync();

        var previousHash = previousRecord?.TransactionHash ?? string.Empty;
        var paymentDate = DateTime.UtcNow;

        // Calcular el hash de transacción
        var transactionHash = CalculateTransactionHash(
            record.Id,
            record.Folio,
            record.Amount,
            record.PatientId,
            paymentDate,
            previousHash);

        record.TransactionHash = transactionHash;
        record.CashierId = cashierId;
        record.PaymentDate = paymentDate;

        _dbContext.PaymentRecords.Add(record);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "FINANCE",
            "PAYMENT_CREATED",
            $"Pago creado: Folio {record.Folio}, Monto {record.Amount:C}",
            cashierId);

        return record;
    }

    /// <summary>
    /// Anula un registro de pago.
    /// </summary>
    public async Task<bool> VoidPaymentRecordAsync(Guid paymentId, Guid voidedBy, string reason)
    {
        var record = await _dbContext.PaymentRecords.FindAsync(paymentId);
        if (record == null || record.IsVoided)
            return false;

        // Registrar en auditoría antes de anular
        await _auditService.LogWarningAsync(
            "FINANCE",
            "PAYMENT_VOIDED",
            $"Pago anulado: Folio {record.Folio}, Monto {record.Amount:C}. Razón: {reason}",
            voidedBy);

        record.IsVoided = true;
        record.VoidReason = reason;
        record.VoidBy = voidedBy;
        record.VoidAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Verifica la integridad de la cadena de transacciones.
    /// </summary>
    public async Task<FinancialIntegrityResult> VerifyFinancialIntegrityAsync()
    {
        var result = new FinancialIntegrityResult();
        var records = await _dbContext.PaymentRecords
            .OrderBy(p => p.PaymentDate)
            .ThenBy(p => p.Id)
            .ToListAsync();

        result.TotalRecords = records.Count;
        string previousHash = string.Empty;

        foreach (var record in records)
        {
            var expectedHash = CalculateTransactionHash(
                record.Id,
                record.Folio,
                record.Amount,
                record.PatientId,
                record.PaymentDate,
                previousHash);

            if (record.TransactionHash != expectedHash)
            {
                result.InvalidRecords++;
                result.Violations.Add($"Folio {record.Folio}: Hash no coincide. Esperado: {expectedHash}, Actual: {record.TransactionHash}");
            }
            else
            {
                result.ValidRecords++;
            }

            previousHash = record.TransactionHash;
        }

        result.IsIntegrityValid = result.InvalidRecords == 0;

        return result;
    }

    /// <summary>
    /// Genera el siguiente folio financiero.
    /// </summary>
    public async Task<string> GenerateNextFolioAsync()
    {
        var lastRecord = await _dbContext.PaymentRecords
            .OrderByDescending(p => p.PaymentDate)
            .FirstOrDefaultAsync();

        if (lastRecord == null)
            return "FOL-000001";

        // Extraer número del folio y incrementar
        var parts = lastRecord.Folio.Split('-');
        if (parts.Length == 2 && int.TryParse(parts[1], out var number))
        {
            return $"FOL-{(number + 1):D6}";
        }

        return $"FOL-{DateTime.UtcNow:yyyyMMdd}-001";
    }

    /// <summary>
    /// Abre una sesión de caja.
    /// </summary>
    public async Task<CashRegisterSession> OpenCashRegisterSessionAsync(Guid cashierId, decimal openingAmount)
    {
        // Obtener la última sesión cerrada del cajero
        var lastClosedSession = await _dbContext.CashRegisterSessions
            .Where(c => c.CashierId == cashierId && c.Status == CashRegisterSessionStatus.Closed)
            .OrderByDescending(c => c.ClosedAt)
            .FirstOrDefaultAsync();

        var session = new CashRegisterSession
        {
            Id = Guid.NewGuid(),
            CashierId = cashierId,
            OpenedAt = DateTime.UtcNow,
            OpeningAmount = openingAmount,
            Status = CashRegisterSessionStatus.Open,
            PreviousSessionId = lastClosedSession?.Id
        };

        _dbContext.CashRegisterSessions.Add(session);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "FINANCE",
            "CASH_SESSION_OPENED",
            $"Sesión de caja abierta por cajero {cashierId}. Monto de apertura: {openingAmount:C}",
            cashierId);

        return session;
    }

    /// <summary>
    /// Cierra una sesión de caja (cierre ciego).
    /// </summary>
    public async Task<CashRegisterSession> CloseCashRegisterSessionAsync(Guid sessionId, decimal declaredAmount)
    {
        var session = await _dbContext.CashRegisterSessions.FindAsync(sessionId);
        if (session == null || session.Status != CashRegisterSessionStatus.Open)
            throw new ArgumentException("Sesión no encontrada o ya cerrada", nameof(sessionId));

        // Calcular el monto de cierre calculado
        var calculatedAmount = await CalculateClosingAmountAsync(sessionId);

        session.DeclaredClosingAmount = declaredAmount;
        session.CalculatedClosingAmount = calculatedAmount;
        session.ClosedAt = DateTime.UtcNow;
        session.Status = CashRegisterSessionStatus.Closed;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "FINANCE",
            "CASH_SESSION_CLOSED",
            $"Sesión de caja cerrada. Declarado: {declaredAmount:C}, Calculado: {calculatedAmount:C}, Diferencia: {session.Difference:C}",
            session.CashierId);

        return session;
    }

    /// <summary>
    /// Obtiene la sesión de caja activa.
    /// </summary>
    public async Task<CashRegisterSession?> GetActiveSessionAsync(Guid cashierId)
    {
        return await _dbContext.CashRegisterSessions
            .FirstOrDefaultAsync(c => c.CashierId == cashierId && c.Status == CashRegisterSessionStatus.Open);
    }

    /// <summary>
    /// Exporta pólizas a CSV.
    /// </summary>
    public async Task<string> ExportPoliciesToCsvAsync(DateTime startDate, DateTime endDate)
    {
        var payments = await _dbContext.PaymentRecords
            .Where(p => p.PaymentDate >= startDate && p.PaymentDate <= endDate && !p.IsVoided)
            .Include(p => p.Concept)
            .OrderBy(p => p.PaymentDate)
            .ToListAsync();

        var csv = new StringBuilder();
        csv.AppendLine("Folio,Concepto,Monto,Descuento,Neto,Método,Fecha,Paciente");

        foreach (var payment in payments)
        {
            var netAmount = payment.Amount - payment.DiscountAmount;
            csv.AppendLine($"{payment.Folio},{payment.Concept?.Name ?? "N/A"},{payment.Amount},{payment.DiscountAmount},{netAmount},{payment.PaymentMethod},{payment.PaymentDate:O},{payment.PatientId}");
        }

        return csv.ToString();
    }

    /// <summary>
    /// Calcula el hash de transacción.
    /// </summary>
    private static string CalculateTransactionHash(Guid id, string folio, decimal amount, Guid? patientId, DateTime paymentDate, string previousHash)
    {
        var data = $"{id}|{folio}|{amount}|{patientId}|{paymentDate:O}|{previousHash}";
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(data);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Calcula el monto de cierre de una sesión.
    /// </summary>
    private async Task<decimal> CalculateClosingAmountAsync(Guid sessionId)
    {
        var session = await _dbContext.CashRegisterSessions.FindAsync(sessionId);
        if (session == null)
            return 0;

        var payments = await _dbContext.PaymentRecords
            .Where(p => p.PaymentDate >= session.OpenedAt && 
                       (!session.ClosedAt.HasValue || p.PaymentDate <= session.ClosedAt.Value) &&
                       !p.IsVoided &&
                       p.PaymentMethod == PaymentMethod.Cash)
            .SumAsync(p => p.Amount - p.DiscountAmount);

        return session.OpeningAmount + payments;
    }
}
