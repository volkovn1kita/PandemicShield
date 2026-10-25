namespace PandemicShield.Api.Reports
{
    /// <summary>Тексти звіту українською та англійською (мова — параметр ?lang=uk|en, як перемикач у застосунку).</summary>
    public sealed class ReportText
    {
        private readonly bool _uk;

        private ReportText(bool uk) => _uk = uk;

        public static ReportText For(string? lang) => new(!string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase));

        public string Lang => _uk ? "uk" : "en";

        private string T(string uk, string en) => _uk ? uk : en;

        public string Title => T("Звіт по зразку", "Sample report");
        public string Disclaimer => T(
            "Дослідницький інструмент, не медичний виріб. Гени й мутації — це прогноз; клінічне рішення ухвалює фахівець " +
            "за лабораторним тестом чутливості.",
            "Research tool, not a medical device. Genes and mutations are predictions; clinical decisions belong to a specialist " +
            "and a laboratory susceptibility test.");

        public string Sample => T("Зразок", "Sample");
        public string File => T("Файл", "File");
        public string RunId => T("Запуск", "Run");
        public string Type => T("Тип зразка", "Sample type");
        public string Received => T("Отримано", "Received");
        public string Records => T("Записів", "Records");
        public string Bases => T("Нуклеотидів", "Nucleotides");
        public string Source => T("Джерело", "Source");
        public string Generated => T("Сформовано", "Generated");
        public string Reads => T("Із сирих прочитань", "From raw reads");
        public string Virus => T("Вірус", "Virus");
        public string Bacterium => T("Бактерія", "Bacterium");
        public string Human => T("Людина", "Human");

        public string Analysis => T("Аналіз і версії", "Analysis and versions");
        public string Dataset => T("Набір даних Nextclade", "Nextclade dataset");
        public string Species => T("Вид (AMRFinderPlus)", "Species (AMRFinderPlus)");
        public string AmrDatabase => T("База AMRFinderPlus", "AMRFinderPlus database");
        public string NotSpecified => T("не вказано", "not specified");

        public string Genomes => T("Геноми", "Genomes");
        public string Genome => T("Геном", "Genome");
        public string Clade => T("Клада", "Clade");
        public string Lineage => T("Лінія", "Lineage");
        public string Qc => T("Якість", "QC");
        public string Coverage => T("Покриття", "Coverage");
        public string Changes => T("Заміни / делеції", "Subst. / del.");
        public string NotAnalysed => T("не проаналізовано", "not analysed");
        public string QcStatus(string? status) => status switch
        {
            "good" => T("добра", "good"),
            "mediocre" => T("посередня", "mediocre"),
            "bad" => T("погана", "bad"),
            _ => "—"
        };

        public string SequenceType => T("Тип штаму (MLST)", "Sequence type (MLST)");
        public string Scheme => T("Схема PubMLST", "PubMLST scheme");
        public string Alleles => T("Алелі", "Alleles");
        public string MlstPending => T("ще не визначено", "not determined yet");

        public string Resistance => T("Прогнозована стійкість", "Predicted resistance");
        public string Drug => T("Антибіотики (підклас)", "Antibiotics (subclass)");
        public string GenesAndMutations => T("Гени та мутації", "Genes and mutations");
        public string Findings => T("Знахідки AMRFinderPlus", "AMRFinderPlus findings");
        public string Gene => T("Ген", "Gene");
        public string ClassCol => T("Клас / підклас", "Class / subclass");
        public string Location => T("Контиг : позиція", "Contig : position");
        public string Identity => T("Ідентичність", "Identity");
        public string Method => T("Метод", "Method");
        public string OtherFindings(int stress, int virulence) =>
            T($"Окрім резистентності: генів стресу — {stress}, вірулентності — {virulence} (у CSV — повний список).",
              $"Besides resistance: {stress} stress and {virulence} virulence genes (full list in the CSV).");
        public string NoResistance => T("Генів резистентності не знайдено.", "No resistance genes found.");

        public string Alerts => T("Сповіщення за правилами", "Rule alerts");
        public string Rule => T("Правило", "Rule");
        public string Variant => T("Варіант", "Variant");
        public string Where => T("Де", "Where");
        public string NoAlerts => T("Жодне правило не спрацювало.", "No rule matched.");

        /// <summary>Підклас AMRFinderPlus людською мовою, як у застосунку (CARBAPENEM → «Карбапенеми» / «Carbapenem»).</summary>
        public string DrugName(string subclass)
        {
            string key = subclass.Replace('_', ' ').Trim().ToUpperInvariant();
            if (_uk && DrugNamesUk.TryGetValue(key, out string? name)) return name;
            string lower = key.ToLowerInvariant();
            return lower.Length == 0 ? lower : char.ToUpperInvariant(lower[0]) + lower[1..];
        }

        // Той самий словник, що _drugNamesUk у pandemic_shield_desktop/lib/pages/amr_view.dart.
        private static readonly Dictionary<string, string> DrugNamesUk = new()
        {
            ["CARBAPENEM"] = "Карбапенеми",
            ["CEPHALOSPORIN"] = "Цефалоспорини",
            ["BETA-LACTAM"] = "Бета-лактами",
            ["METHICILLIN"] = "Метицилін",
            ["COLISTIN"] = "Колістин",
            ["VANCOMYCIN"] = "Ванкоміцин",
            ["AMINOGLYCOSIDE"] = "Аміноглікозиди",
            ["GENTAMICIN"] = "Гентаміцин",
            ["AMIKACIN"] = "Амікацин",
            ["TOBRAMYCIN"] = "Тобраміцин",
            ["STREPTOMYCIN"] = "Стрептоміцин",
            ["KANAMYCIN"] = "Канаміцин",
            ["SULFONAMIDE"] = "Сульфаніламіди",
            ["TRIMETHOPRIM"] = "Триметоприм",
            ["TETRACYCLINE"] = "Тетрацикліни",
            ["QUINOLONE"] = "Хінолони",
            ["CIPROFLOXACIN"] = "Ципрофлоксацин",
            ["MACROLIDE"] = "Макроліди",
            ["AZITHROMYCIN"] = "Азитроміцин",
            ["ERYTHROMYCIN"] = "Еритроміцин",
            ["TELITHROMYCIN"] = "Телітроміцин",
            ["CLINDAMYCIN"] = "Кліндаміцин",
            ["LINEZOLID"] = "Лінезолід",
            ["DAPTOMYCIN"] = "Даптоміцин",
            ["TIGECYCLINE"] = "Тигециклін",
            ["CEFTAZIDIME"] = "Цефтазидим",
            ["CEFTAZIDIME-AVIBACTAM"] = "Цефтазидим-авібактам",
            ["MUPIROCIN"] = "Мупіроцин",
            ["FUSIDIC ACID"] = "Фузидова кислота",
            ["PHENICOL"] = "Феніколи",
            ["LINCOSAMIDE"] = "Лінкозаміди",
            ["STREPTOGRAMIN"] = "Стрептограміни",
            ["GLYCOPEPTIDE"] = "Глікопептиди",
            ["POLYMYXIN"] = "Поліміксини",
            ["NITROFURANTOIN"] = "Нітрофурантоїн",
            ["FOSFOMYCIN"] = "Фосфоміцин",
            ["CHLORAMPHENICOL"] = "Хлорамфенікол",
            ["FLORFENICOL"] = "Флорфенікол",
            ["RIFAMYCIN"] = "Рифаміцини",
            ["EFFLUX"] = "Ефлюкс-насоси (кілька груп)",
            ["PIPERACILLIN-TAZOBACTAM"] = "Піперацилін-тазобактам",
            ["AMOXICILLIN-CLAVULANIC ACID"] = "Амоксицилін-клавуланат",
            ["TICARCILLIN-CLAVULANIC ACID"] = "Тикарцилін-клавуланат",
        };

        public string Page => T("Сторінка", "Page");
        public string Footer => T("PandemicShield · відкритий код · дані не залишали цей комп'ютер",
            "PandemicShield · open source · data never left this computer");
    }
}
