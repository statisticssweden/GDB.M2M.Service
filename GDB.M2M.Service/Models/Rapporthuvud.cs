using System;
using System.Collections.Generic;
using System.Linq;

namespace GDB.M2M.Service.Models
{
    public class Rapporthuvud
    {
        public Rapporthuvud()
        {
            Rapportrader = new List<Rapportrad>();
        }

        public Guid Id { get; set; }
        public string UppgiftslamnareNamn { get; set; }
        public string UndersokningNamn { get; set; }
        public string BlankettNamn { get; set; }
        public string Referensperiod { get; set; }
        public DateTime InskickatDateTime { private get; set; }
        public string InskickatDatum { get { return InskickatDateTime.ToString("yyyy-MM-dd HH:mm"); } }
        public string Status { get; set; }
        public ProgresStatus ProgressStatus { get; set; }
        public bool HasChildren { get { return Rapportrader.Any(); } }
        public bool IsExpanded { get; set; }
        public List<Rapportrad> Rapportrader { get; set; }

        public Guid LatestInskickningsId { get; set; }
    }

    public class Rapportrad
    {
        public Guid Id { get; set; }
        public Guid ParentId { get; set; }
        public string UppgiftslamnareNamn { get { return ""; } }
        public string UndersokningNamn { get { return ""; } }
        public string BlankettNamn { get { return ""; } }
        public string Referensperiod { get { return ""; } }
        public DateTime InskickatDateTime { private get; set; }
        public string InskickatDatum { get { return InskickatDateTime.ToString("yyyy-MM-dd HH:mm"); } }
        public string Status { get; set; }
        public ProgresStatus ProgressStatus { get; set; }
        public bool HasChildren { get { return false; } }
    }

    public enum ProgresStatus
    {
        EjPaborjad = 0,
        Pagar = 1,
        AvslutadFel = 2,
        AvslutadOk = 3
    }
}