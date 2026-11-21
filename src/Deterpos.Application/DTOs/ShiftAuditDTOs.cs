using System;
using System.Collections.Generic;
using Deterpos.Domain.Entities;

namespace Deterpos.Application.DTOs;

public class ShiftBlindAuditDeclaration
{
    public int ShiftId { get; set; }
    public decimal DeclaredCashUSD { get; set; }
    public decimal DeclaredCashVES { get; set; }
    public decimal DeclaredPosVES { get; set; }
    public decimal DeclaredPagoMovilVES { get; set; }
    public decimal DeclaredZelleUSD { get; set; }
    public decimal DeclaredTransferVES { get; set; }
    public decimal DeclaredOtherUSD { get; set; }
    public decimal DeclaredOtherVES { get; set; }
    public string ClosingNotes { get; set; } = string.Empty;
}

public class ShiftPaymentAuditItem
{
    public string MethodName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "VES";
    public decimal ExpectedAmount { get; set; }
    public decimal DeclaredAmount { get; set; }
    public decimal Difference => decimal.Round(DeclaredAmount - ExpectedAmount, 2);
    public string Status => Difference == 0m ? "CUADRADO" : (Difference > 0m ? "SOBRANTE" : "FALTANTE");
    public bool IsBalanced => Difference == 0m;
}

public class ShiftAuditSummaryResult
{
    public CashShift Shift { get; set; } = null!;
    public List<ShiftPaymentAuditItem> AuditItems { get; set; } = new();
    public decimal TotalExpectedCashUSD { get; set; }
    public decimal TotalDeclaredCashUSD { get; set; }
    public decimal DifferenceCashUSD => decimal.Round(TotalDeclaredCashUSD - TotalExpectedCashUSD, 2);

    public decimal TotalExpectedCashVES { get; set; }
    public decimal TotalDeclaredCashVES { get; set; }
    public decimal DifferenceCashVES => decimal.Round(TotalDeclaredCashVES - TotalExpectedCashVES, 2);

    public decimal TotalElectronicExpectedVES { get; set; }
    public decimal TotalElectronicDeclaredVES { get; set; }
    public decimal DifferenceElectronicVES => decimal.Round(TotalElectronicDeclaredVES - TotalElectronicExpectedVES, 2);

    public string FormattedActText { get; set; } = string.Empty;
}
