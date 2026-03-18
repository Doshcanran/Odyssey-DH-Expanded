using System.Text;
using RimWorld;
using Verse;
using ShipPropulsionUtility = InterstellarOdyssey.ShipPropulsionUtility;
namespace InterstellarOdyssey
{
    public static class InterstellarDiagnostics
    {
        public static void RecordInfo(string category, string title, string message, string details = null)
        {
            Record(category, title, message, details, InterstellarDiagnosticSeverity.Info);
        }

        public static void RecordWarning(string category, string title, string message, string details = null)
        {
            Record(category, title, message, details, InterstellarDiagnosticSeverity.Warning);
        }

        public static void RecordError(string category, string title, string message, string details = null)
        {
            Record(category, title, message, details, InterstellarDiagnosticSeverity.Error);
        }

        public static void Record(string category, string title, string message, string details, InterstellarDiagnosticSeverity severity)
        {
            WorldComponent_Interstellar component = Find.World?.GetComponent<WorldComponent_Interstellar>();
            component?.AddDiagnostic(category, title, message, details, severity);

            string prefix = "[InterstellarOdyssey][" + (category ?? "General") + "] " + (title ?? "Запись") + ": " + (message ?? string.Empty);
            if (!string.IsNullOrEmpty(details))
                prefix += "\n" + details;

            switch (severity)
            {
                case InterstellarDiagnosticSeverity.Error:
                    Log.Error(prefix);
                    break;
                case InterstellarDiagnosticSeverity.Warning:
                    Log.Warning(prefix);
                    break;
                default:
                    Log.Message(prefix);
                    break;
            }
        }

        public static string BuildLaunchDiagnosticReport(WorldComponent_Interstellar data, Thing ship, OrbitalNode source, OrbitalNode destination)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== Диагностика запуска ===");
            sb.AppendLine("Корабль: " + (ship != null ? ship.LabelCap : "не найден"));
            sb.AppendLine("Текущий узел: " + (source != null ? source.label : "неизвестно"));
            sb.AppendLine("Цель: " + (destination != null ? destination.label : "не выбрана"));
            sb.AppendLine();

            if (ship == null)
            {
                sb.AppendLine("Ошибка: якорь корабля не найден.");
                return sb.ToString();
            }

            if (data != null)
                sb.AppendLine("Сохранённое местоположение: " + data.GetCurrentNodeIdForShip(ship));

            ShipValidationReport validation = ShipValidationUtility.ValidateForLaunch(ship);
            sb.AppendLine("--- Проверки запуска ---");
            sb.AppendLine(validation.ToUserText());
            sb.AppendLine();

            if (ShipCaptureUtility.TryCollectShipClusterDebugData(ship, out var shipCells, out var perimeterCells, out var bounds, out var summary))
            {
                sb.AppendLine("--- Геометрия кластера ---");
                sb.AppendLine(summary);
                sb.AppendLine("Палуба: " + (shipCells != null ? shipCells.Count.ToString() : "0"));
                sb.AppendLine("Периметр: " + (perimeterCells != null ? perimeterCells.Count.ToString() : "0"));
                sb.AppendLine("Bounds: " + bounds);
                sb.AppendLine();

                if (ShipCaptureUtility.TryCollectShipCluster(ship, out ShipClusterData cluster))
                {
                    sb.AppendLine("--- Состав ---");
                    sb.AppendLine("Структур: " + cluster.structuralThings.Count);
                    sb.AppendLine("Предметов: " + cluster.items.Count);
                    sb.AppendLine("Пешек: " + cluster.pawns.Count);
                    sb.AppendLine();

                    if (source != null && destination != null)
                    {
                        ShipPropulsionReport propulsion = ShipPropulsionUtility.Evaluate(cluster, source, destination);
                        sb.AppendLine("--- Движение ---");
                        sb.AppendLine("Масса: " + propulsion.totalMass.ToString("0.#"));
                        sb.AppendLine("Тяга: " + propulsion.totalThrust.ToString("0.#"));
                        sb.AppendLine("Топливо всего: " + propulsion.totalFuel.ToString("0.#"));
                        sb.AppendLine("Нужно топлива: " + propulsion.fuelNeeded.ToString("0.#"));
                        sb.AppendLine("Оценка длительности: " + propulsion.travelDays.ToString("0.00") + " д.");
                        sb.AppendLine("Тяги достаточно: " + propulsion.hasEnoughThrust);
                        sb.AppendLine("Топлива достаточно: " + propulsion.hasEnoughFuel);
                        sb.AppendLine();
                    }
                }
            }
            else
            {
                sb.AppendLine("--- Геометрия кластера ---");
                sb.AppendLine("Не удалось собрать debug-данные.");
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }
    }
}
