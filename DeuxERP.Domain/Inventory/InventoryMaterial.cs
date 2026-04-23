namespace DeuxERP.Domain.Inventory;

public class InventoryMaterial
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public long UnitCost { get; private set; }
    public MeasureUnit MeasureUnit { get; private set; }
    public bool Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public InventoryMaterial(string name, int quantity, long totalCost, MeasureUnit measureUnit)
    {
        Name = NormalizeName(name);

        if (quantity <= 0)
            throw new ArgumentException("A quantidade deve ser maior que zero.", nameof(quantity));

        if (totalCost <= 0)
            throw new ArgumentException("O custo total deve ser maior que zero.", nameof(totalCost));

        ValidateMeasureUnit(measureUnit);

        Id = Guid.NewGuid();
        Quantity = quantity;
        UnitCost = totalCost / quantity;
        MeasureUnit = measureUnit;
        Status = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    private InventoryMaterial() { }

    public void Update(string name, MeasureUnit measureUnit)
    {
        Name = NormalizeName(name);
        ValidateMeasureUnit(measureUnit);
        MeasureUnit = measureUnit;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Restock(int addedQuantity, long addedTotalCost)
    {
        if (addedQuantity <= 0)
            throw new ArgumentException("A quantidade adicionada deve ser maior que zero.", nameof(addedQuantity));

        if (addedTotalCost <= 0)
            throw new ArgumentException("O custo total adicionado deve ser maior que zero.", nameof(addedTotalCost));

        var existingTotal = (long)Quantity * UnitCost;
        var newQuantity = Quantity + addedQuantity;

        Quantity = newQuantity;
        if (newQuantity != 0)
            UnitCost = (existingTotal + addedTotalCost) / newQuantity;

        UpdatedAt = DateTime.UtcNow;
    }

    public int AdjustQuantity(int delta)
    {
        Quantity += delta;
        UpdatedAt = DateTime.UtcNow;
        return Quantity;
    }

    public void ChangeStatus()
    {
        Status = !Status;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Nome não pode ser vazio.", nameof(name));

        var normalized = name.Trim();
        if (normalized.Length > 200)
            throw new ArgumentException("Nome não pode exceder 200 caracteres.", nameof(name));

        return normalized;
    }

    private static void ValidateMeasureUnit(MeasureUnit measureUnit)
    {
        if (!Enum.IsDefined(measureUnit))
            throw new ArgumentException("Unidade de medida inválida.", nameof(measureUnit));
    }
}
