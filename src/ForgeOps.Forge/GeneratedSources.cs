namespace ForgeOps.Forge;

/// <summary>
/// Source files ForgeOps controls and always injects into a generated-code compilation:
/// the fixed domain contract the model must implement, a tiny assertion kit, and the
/// deterministic acceptance suite (ProjectForge.md §2.2, §31 — the broken scenario must be
/// genuinely detectable).
/// </summary>
public static class GeneratedSources
{
    /// <summary>The interface + value types the model implements. Given to the model verbatim.</summary>
    public const string Contract =
        """
        namespace CustomerHub.Loyalty;

        public sealed record Order(string OrderId, string CustomerId, decimal NetTotal, bool IsPaid);

        public sealed record LedgerEntry(string OrderId, string CustomerId, int Points, string Reason, System.DateTimeOffset At);

        public interface ILoyaltyService
        {
            /// Award points for a confirmed-paid order. MUST be idempotent per OrderId.
            void OnPaymentConfirmed(Order order);

            /// Reverse points previously awarded for an order (full refund). No-op if none were awarded.
            void OnOrderRefunded(string orderId);

            int BalanceFor(string customerId);

            System.Collections.Generic.IReadOnlyList<LedgerEntry> Ledger { get; }
        }
        """;

    /// <summary>Assertion kit. Compiled into every generated assembly under <c>ForgeOps.Generated</c>.</summary>
    public const string TestKit =
        """
        namespace ForgeOps.Generated;

        using System;
        using System.Collections.Generic;

        [AttributeUsage(AttributeTargets.Method)]
        public sealed class ForgeFactAttribute : Attribute
        {
            public string? Name { get; set; }
        }

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
        public sealed class CriterionAttribute : Attribute
        {
            public CriterionAttribute(string id) => Id = id;
            public string Id { get; }
        }

        [AttributeUsage(AttributeTargets.Method)]
        public sealed class ForgeScenarioAttribute : Attribute { }

        /// Collects the action → output pairs of a scripted walkthrough for display.
        public static class Scenario
        {
            public static readonly List<string[]> Steps = new();

            public static void Step(string action, string output) => Steps.Add(new[] { action, output ?? "" });
        }

        public sealed class ForgeAssertException : Exception
        {
            public ForgeAssertException(string message) : base(message) { }
        }

        public static class Check
        {
            public static void True(bool condition, string? message = null)
            {
                if (!condition) throw new ForgeAssertException(message ?? "Expected condition to be true.");
            }

            public static void False(bool condition, string? message = null)
            {
                if (condition) throw new ForgeAssertException(message ?? "Expected condition to be false.");
            }

            public static void Equal<T>(T expected, T actual)
            {
                if (!EqualityComparer<T>.Default.Equals(expected, actual))
                    throw new ForgeAssertException($"Expected <{expected}>, got <{actual}>.");
            }

            public static void NotNull(object? value)
            {
                if (value is null) throw new ForgeAssertException("Expected a non-null value.");
            }

            public static void Throws<TException>(Action action) where TException : Exception
            {
                try { action(); }
                catch (TException) { return; }
                catch (Exception ex)
                {
                    throw new ForgeAssertException($"Expected {typeof(TException).Name}, got {ex.GetType().Name}.");
                }
                throw new ForgeAssertException($"Expected {typeof(TException).Name}, but nothing was thrown.");
            }
        }
        """;

    /// <summary>
    /// ForgeOps' own acceptance suite for the loyalty requirement. Deterministic, authored
    /// here (not by the model), each test tagged with the criterion it proves. AC-2 is the
    /// §31 duplicate-event bug — a weak implementation passes its own tests but fails this.
    /// </summary>
    public const string CanonicalSuite =
        """
        namespace CustomerHub.Loyalty.CanonicalTests;

        using System;
        using System.Linq;
        using CustomerHub.Loyalty;
        using ForgeOps.Generated;

        public static class LoyaltyAcceptance
        {
            private static Order Paid(string id, string customer, decimal total) => new(id, customer, total, true);

            [ForgeFact, Criterion("AC-1")]
            public static void Awards_floor_of_net_total_on_paid_order()
            {
                var svc = new LoyaltyService();
                svc.OnPaymentConfirmed(Paid("o1", "c1", 42.90m));
                Check.Equal(42, svc.BalanceFor("c1"));
            }

            [ForgeFact, Criterion("AC-2")]
            public static void Duplicate_payment_event_credits_points_at_most_once()
            {
                var svc = new LoyaltyService();
                var order = Paid("o1", "c1", 42.90m);
                svc.OnPaymentConfirmed(order);
                svc.OnPaymentConfirmed(order); // redelivered event
                Check.Equal(42, svc.BalanceFor("c1"));
            }

            [ForgeFact, Criterion("AC-3")]
            public static void Full_refund_reverses_awarded_points()
            {
                var svc = new LoyaltyService();
                svc.OnPaymentConfirmed(Paid("o1", "c1", 42.90m));
                svc.OnOrderRefunded("o1");
                Check.Equal(0, svc.BalanceFor("c1"));
            }

            [ForgeFact, Criterion("AC-4")]
            public static void Below_minimum_qualifying_value_awards_nothing()
            {
                var svc = new LoyaltyService();
                svc.OnPaymentConfirmed(Paid("o1", "c1", 0.50m));
                Check.Equal(0, svc.BalanceFor("c1"));
            }

            [ForgeFact, Criterion("AC-5")]
            public static void Crediting_writes_an_audit_entry_for_the_order()
            {
                var svc = new LoyaltyService();
                svc.OnPaymentConfirmed(Paid("o-42", "c1", 10m));
                var entry = svc.Ledger.FirstOrDefault(e => e.OrderId == "o-42");
                Check.NotNull(entry);
                Check.True(!string.IsNullOrWhiteSpace(entry!.Reason), "Ledger entry needs a reason.");
            }

            [ForgeFact, Criterion("AC-1")]
            public static void Unpaid_order_awards_nothing()
            {
                var svc = new LoyaltyService();
                svc.OnPaymentConfirmed(new Order("o1", "c1", 40m, false));
                Check.Equal(0, svc.BalanceFor("c1"));
            }
        }
        """;

    /// <summary>
    /// ForgeOps' known-good implementation of the contract. Used only as a labelled
    /// fallback when the model's output does not compile within the repair budget
    /// (ProjectForge.md §49 — never invent results; the substitution is disclosed).
    /// </summary>
    public const string ReferenceImplementation =
        """
        namespace CustomerHub.Loyalty;

        using System;
        using System.Collections.Generic;

        public sealed class LoyaltyService : ILoyaltyService
        {
            private const decimal MinimumQualifyingValue = 1.00m;
            private readonly Dictionary<string, (string CustomerId, int Points)> _awarded = new();
            private readonly Dictionary<string, int> _balances = new();
            private readonly List<LedgerEntry> _ledger = new();

            public IReadOnlyList<LedgerEntry> Ledger => _ledger;

            public void OnPaymentConfirmed(Order order)
            {
                if (!order.IsPaid || order.NetTotal < MinimumQualifyingValue)
                    return;
                if (_awarded.ContainsKey(order.OrderId))
                    return;

                var points = (int)Math.Floor(order.NetTotal);
                _awarded[order.OrderId] = (order.CustomerId, points);
                AddPoints(order.CustomerId, points);
                _ledger.Add(new LedgerEntry(order.OrderId, order.CustomerId, points, "purchase", DateTimeOffset.UtcNow));
            }

            public void OnOrderRefunded(string orderId)
            {
                if (!_awarded.TryGetValue(orderId, out var award))
                    return;

                _awarded.Remove(orderId);
                AddPoints(award.CustomerId, -award.Points);
                _ledger.Add(new LedgerEntry(orderId, award.CustomerId, -award.Points, "refund", DateTimeOffset.UtcNow));
            }

            public int BalanceFor(string customerId) =>
                _balances.TryGetValue(customerId, out var balance) ? balance : 0;

            private void AddPoints(string customerId, int delta) =>
                _balances[customerId] = BalanceFor(customerId) + delta;
        }
        """;

    /// <summary>Minimal tests paired with <see cref="ReferenceImplementation"/> for the fallback path.</summary>
    public const string ReferenceTests =
        """
        namespace CustomerHub.Loyalty.Tests;

        using System.Linq;
        using CustomerHub.Loyalty;
        using ForgeOps.Generated;

        public static class LoyaltyServiceTests
        {
            [ForgeFact]
            public static void Awards_and_reverses()
            {
                var svc = new LoyaltyService();
                svc.OnPaymentConfirmed(new Order("o1", "c1", 30.75m, true));
                Check.Equal(30, svc.BalanceFor("c1"));
                svc.OnOrderRefunded("o1");
                Check.Equal(0, svc.BalanceFor("c1"));
            }

            [ForgeFact]
            public static void Ledger_records_the_award()
            {
                var svc = new LoyaltyService();
                svc.OnPaymentConfirmed(new Order("o1", "c1", 30.75m, true));
                Check.NotNull(svc.Ledger.FirstOrDefault(e => e.OrderId == "o1"));
            }
        }
        """;

    /// <summary>
    /// A scripted walkthrough that ForgeOps runs against the generated <c>LoyaltyService</c>
    /// to show it working with concrete inputs and outputs (the "visual output" surface).
    /// Authored by ForgeOps, executed in the sandbox against the model's own code.
    /// </summary>
    public const string ScenarioSuite =
        """
        namespace CustomerHub.Loyalty.Walkthrough;

        using System;
        using System.Linq;
        using CustomerHub.Loyalty;
        using ForgeOps.Generated;

        public static class LoyaltyWalkthrough
        {
            [ForgeScenario]
            public static void Run()
            {
                var svc = new LoyaltyService();

                var order = new Order("ORD-1001", "alice", 42.90m, true);
                svc.OnPaymentConfirmed(order);
                Scenario.Step(
                    "OnPaymentConfirmed( ORD-1001 · alice · $42.90 · paid )",
                    $"alice balance = {svc.BalanceFor("alice")} points");

                svc.OnPaymentConfirmed(order);
                Scenario.Step(
                    "OnPaymentConfirmed( ORD-1001 ) again  — duplicate webhook delivery",
                    $"alice balance = {svc.BalanceFor("alice")} points  (unchanged — idempotent)");

                svc.OnPaymentConfirmed(new Order("ORD-1002", "bob", 0.80m, true));
                Scenario.Step(
                    "OnPaymentConfirmed( ORD-1002 · bob · $0.80 · paid )  — below the $1.00 minimum",
                    $"bob balance = {svc.BalanceFor("bob")} points");

                svc.OnPaymentConfirmed(new Order("ORD-1003", "carol", 120.00m, false));
                Scenario.Step(
                    "OnPaymentConfirmed( ORD-1003 · carol · $120.00 · NOT paid )",
                    $"carol balance = {svc.BalanceFor("carol")} points");

                svc.OnOrderRefunded("ORD-1001");
                Scenario.Step(
                    "OnOrderRefunded( ORD-1001 )  — alice's order is fully refunded",
                    $"alice balance = {svc.BalanceFor("alice")} points");

                var ledger = string.Join(
                    "\n",
                    svc.Ledger.Select(e => $"  {e.OrderId,-10} {e.CustomerId,-6} {(e.Points >= 0 ? "+" : "")}{e.Points,-4} {e.Reason}"));
                Scenario.Step("Final loyalty ledger", ledger.Length == 0 ? "(empty)" : "\n" + ledger);
            }
        }
        """;

    /// <summary>Criterion id → statement, for mapping run results back to the spec (§15).</summary>
    public static readonly IReadOnlyDictionary<string, string> CriteriaStatements = new Dictionary<string, string>
    {
        ["AC-1"] = "A paid order credits floor(net total) points to the customer.",
        ["AC-2"] = "A redelivered payment-confirmed event credits points at most once.",
        ["AC-3"] = "A full refund reverses the points that were awarded.",
        ["AC-4"] = "A purchase below the minimum qualifying value credits nothing.",
        ["AC-5"] = "Crediting writes an audit entry recording the order and reason.",
    };
}
