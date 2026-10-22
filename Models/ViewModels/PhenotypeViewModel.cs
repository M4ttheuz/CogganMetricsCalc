namespace inzynierka.Models.ViewModels;

public class PhenotypeViewModel
{
    public string PhenotypeName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string BadgeColor { get; set; } = "primary";

    public double Peak5sWkg { get; set; }
    public double Peak1mWkg { get; set; }
    public double Peak5mWkg { get; set; }
    public double FtpWkg { get; set; }

    public int SprintScore { get; set; }
    public int AnaerobicScore { get; set; }
    public int Vo2MaxScore { get; set; }
    public int ThresholdScore { get; set; }
}