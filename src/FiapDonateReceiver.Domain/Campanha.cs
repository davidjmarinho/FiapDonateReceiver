namespace FiapDonateReceiver.Domain;

public class Campanha
{
    public Guid Id { get; set; }
    public CampanhaStatus Status { get; set; }
    public decimal ValorArrecadado { get; set; }
}
