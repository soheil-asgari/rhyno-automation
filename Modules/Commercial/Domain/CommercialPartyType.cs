namespace OfficeAutomation.Modules.Commercial.Domain;

/// <summary>
/// Type of a commercial party. A single party can act as both a customer (sales)
/// and a supplier (purchases), avoiding duplicate master data.
/// </summary>
public enum CommercialPartyType
{
    Customer = 1,
    Supplier = 2,
    Both = 3
}