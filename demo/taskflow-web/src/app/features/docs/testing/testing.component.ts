import { Component, inject, signal, OnInit, OnDestroy, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { CodeHighlighterService } from '../../../core/services/code-highlighter.service';
import { ThemeService } from '../../../core/services/theme.service';

interface NavItem {
  id: string;
  label: string;
  icon: string;
}

@Component({
  selector: 'app-testing-docs',
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule
  ],
  templateUrl: './testing.component.html',
  styleUrl: './testing.component.css'
})
export class TestingComponent implements OnInit, OnDestroy {
  private readonly codeHighlighter = inject(CodeHighlighterService);
  private readonly themeService = inject(ThemeService);
  private readonly sanitizer = inject(DomSanitizer);
  private intersectionObserver: IntersectionObserver | null = null;
  private visibleSections = new Set<string>();

  readonly navItems: NavItem[] = [
    { id: 'overview', label: 'Overview', icon: 'info' },
    { id: 'installation', label: 'Installation', icon: 'download' },
    { id: 'test-setup', label: 'TestSetup', icon: 'settings' },
    { id: 'aggregates', label: 'Testing Aggregates', icon: 'category' },
    { id: 'projections', label: 'Testing Projections', icon: 'view_quilt' },
    { id: 'time-testing', label: 'Time Testing', icon: 'schedule' },
    { id: 'patterns', label: 'Test Patterns', icon: 'pattern' },
    { id: 'performance', label: 'Performance', icon: 'speed' },
    { id: 'best-practices', label: 'Best Practices', icon: 'verified' },
    { id: 'complete-example', label: 'Complete Example', icon: 'integration_instructions' }
  ];

  activeSection = signal<string>('overview');

  installCodeHtml = signal<SafeHtml>('');
  testSetupCodeHtml = signal<SafeHtml>('');
  testSetupAlternativeCodeHtml = signal<SafeHtml>('');
  pattern1CodeHtml = signal<SafeHtml>('');
  pattern2CodeHtml = signal<SafeHtml>('');
  aggregateTestCodeHtml = signal<SafeHtml>('');
  assertionsCodeHtml = signal<SafeHtml>('');
  projectionTestCodeHtml = signal<SafeHtml>('');
  projectionPattern1CodeHtml = signal<SafeHtml>('');
  projectionPattern2CodeHtml = signal<SafeHtml>('');
  timeTestingCodeHtml = signal<SafeHtml>('');
  givenEventsCodeHtml = signal<SafeHtml>('');
  invariantTestCodeHtml = signal<SafeHtml>('');
  performanceCodeHtml = signal<SafeHtml>('');
  completeExampleCodeHtml = signal<SafeHtml>('');

  private readonly installCode = `# Add to your test project
dotnet add package ErikLieben.FA.ES.Testing`;

  private readonly testSetupCode = `using ErikLieben.FA.ES.Testing;
using Xunit;

public class CustomerTests
{
    [Fact]
    public async Task Can_confirm_customer_email()
    {
        // Arrange - Create test context with in-memory event store
        var context = TestSetup.GetContext();

        // Generic method - uses ITestableAggregate.ObjectName automatically
        var stream = await context.GetEventStreamFor<Customer>("cust-123");

        // Given: Set up initial state by appending events
        await context.AppendEvents(stream, new IEvent[]
        {
            new CustomerRegistered("John Doe", "john@example.com", DateTime.UtcNow)
        });

        // Create aggregate and replay events to rebuild state
        var customer = new Customer(stream);
        await customer.Fold();

        // Act
        await customer.ConfirmEmail();

        // Assert - Generic assertion (type-safe, no magic strings)
        context.Assert
            .ShouldHaveObject<Customer>("cust-123")
            .WithEventCount(2)
            .WithEventAtLastPosition(new EmailConfirmed(DateTime.UtcNow));
    }
}`;

  private readonly testSetupAlternativeCode = `// String-based approach - for aggregates without ITestableAggregate
public class LegacyOrderTests
{
    [Fact]
    public async Task Can_ship_legacy_order()
    {
        var context = TestSetup.GetContext();

        // Explicitly specify the object name as a string
        var stream = await context.GetEventStreamFor("LegacyOrder", "order-123");

        // Given: Set up initial state
        await context.AppendEvents(stream, new IEvent[]
        {
            new OrderPlaced("customer-1", DateTime.UtcNow),
            new OrderItemAdded("prod-1", 2, 19.99m, DateTime.UtcNow)
        });

        // Create aggregate and replay events
        var order = new LegacyOrder(stream);
        await order.Fold();

        // Act
        await order.Ship("FedEx", "TRACK-123");

        // String-based assertion
        context.Assert
            .ShouldHaveObject("LegacyOrder", "order-123")
            .WithEventCount(3);
    }
}`;

  private readonly aggregateTestCode = `public class OrderTests
{
    [Fact]
    public async Task Can_add_items_to_existing_order()
    {
        // Arrange
        var context = TestSetup.GetContext();
        var stream = await context.GetEventStreamFor<Order>("order-1");

        // Given: Set up initial state by appending events BEFORE creating aggregate
        await context.AppendEvents(stream, new IEvent[]
        {
            new OrderPlaced("customer-123", DateTime.UtcNow)
        });

        // Create aggregate and replay events
        var order = new Order(stream);
        await order.Fold();

        // Act - Execute domain command on existing aggregate
        await order.AddItem("product-1", 2, 19.99m);
        await order.AddItem("product-2", 1, 29.99m);

        // Assert - Verify events
        context.Assert
            .ShouldHaveObject<Order>("order-1")
            .WithEventCount(3);  // 1 given + 2 from commands

        // Assert state
        Assert.Equal("customer-123", order.CustomerId);
        Assert.Equal(2, order.Items.Count);
        Assert.Equal(69.97m, order.TotalAmount);
    }
}`;

  private readonly assertionsCode = `// === Preferred: Generic methods (type-safe, AOT-friendly) ===

// Verify object exists with specific event count
context.Assert
    .ShouldHaveObject<Customer>("cust-123")
    .WithEventCount(3);

// Verify specific event at position
context.Assert
    .ShouldHaveObject<Order>("order-1")
    .WithEventAtPosition(1, new OrderItemAdded("prod-1", 2, 19.99m));

// Verify last event
context.Assert
    .ShouldHaveObject<Order>("order-1")
    .WithEventAtLastPosition(new OrderShipped("FedEx", "TRACK123"));

// Chain multiple assertions
context.Assert
    .ShouldHaveObject<Customer>("cust-123")
    .WithEventCount(2)
    .WithEventAtPosition(0, new CustomerRegistered(...))
    .WithEventAtLastPosition(new CustomerEmailUpdated(...));

// === Alternative: String-based methods ===
// Use when aggregate doesn't implement ITestableAggregate

context.Assert
    .ShouldHaveObject("Customer", "cust-123")
    .WithEventCount(3);`;

  private readonly projectionTestCode = `public class ActiveWorkItemsProjectionTests
{
    [Fact]
    public async Task Should_track_active_work_items()
    {
        // Arrange
        var context = TestSetup.GetContext();

        // Given: Set up events for a work item
        var stream = await context.GetEventStreamFor<WorkItem>("wi-1");
        await context.AppendEvents(stream, new IEvent[]
        {
            new WorkItemOpened("wi-1", "Fix bug", DateTime.UtcNow),
            new WorkItemAssigned("wi-1", "dev-1", DateTime.UtcNow)
        });

        // Create and update projection
        var projection = new ActiveWorkItems();
        await projection.UpdateToLatestVersion();

        // Assert
        Assert.Single(projection.Items);
        Assert.Equal("Fix bug", projection.Items[0].Title);
        Assert.Equal("dev-1", projection.Items[0].AssignedTo);
    }

    [Fact]
    public async Task Should_remove_completed_items()
    {
        // Arrange
        var context = TestSetup.GetContext();

        // Given: Two active work items
        var stream1 = await context.GetEventStreamFor<WorkItem>("wi-1");
        await context.AppendEvents(stream1, new IEvent[]
        {
            new WorkItemOpened("wi-1", "Task 1", DateTime.UtcNow)
        });

        var stream2 = await context.GetEventStreamFor<WorkItem>("wi-2");
        await context.AppendEvents(stream2, new IEvent[]
        {
            new WorkItemOpened("wi-2", "Task 2", DateTime.UtcNow)
        });

        var projection = new ActiveWorkItems();
        await projection.UpdateToLatestVersion();
        Assert.Equal(2, projection.Items.Count);

        // When: Complete one item
        await context.AppendEvents(stream1, new IEvent[]
        {
            new WorkItemCompleted("wi-1", DateTime.UtcNow)
        });
        await projection.UpdateToLatestVersion();

        // Then: Only uncompleted item remains
        Assert.Single(projection.Items);
        Assert.Equal("wi-2", projection.Items[0].Id);
    }
}`;

  private readonly givenEventsCode = `[Fact]
public async Task Can_ship_order_with_items()
{
    // Given: An order with items
    var context = TestSetup.GetContext();
    var stream = await context.GetEventStreamFor("Order", "order-1");

    await context.AppendEvents(stream, new IEvent[]
    {
        new OrderPlaced("cust-1", DateTime.UtcNow),
        new OrderItemAdded("prod-1", 2, 19.99m, DateTime.UtcNow),
        new OrderItemAdded("prod-2", 1, 29.99m, DateTime.UtcNow)
    });

    // Load aggregate (replays events to rebuild state)
    var order = new Order(stream);
    await order.Fold();

    // Verify initial state
    Assert.Equal(2, order.Items.Count);

    // When: Ship the order
    await order.Ship("FedEx", "TRACK123");

    // Then: Verify shipping event emitted
    context.Assert
        .ShouldHaveObject("Order", "order-1")
        .WithEventCount(4)
        .WithEventAtLastPosition(new OrderShipped("FedEx", "TRACK123"));
}`;

  private readonly invariantTestCode = `[Fact]
public async Task Cannot_ship_empty_order()
{
    // Given: An order with no items
    var context = TestSetup.GetContext();
    var stream = await context.GetEventStreamFor("Order", "order-1");

    await context.AppendEvents(stream, new IEvent[]
    {
        new OrderPlaced("cust-1", DateTime.UtcNow)
    });

    var order = new Order(stream);
    await order.Fold();

    // When/Then: Shipping should throw
    await Assert.ThrowsAsync<InvalidOperationException>(
        () => order.Ship("FedEx", "TRACK123"));
}

[Fact]
public async Task Cannot_add_items_to_shipped_order()
{
    // Given: A shipped order
    var context = TestSetup.GetContext();
    var stream = await context.GetEventStreamFor("Order", "order-1");

    await context.AppendEvents(stream, new IEvent[]
    {
        new OrderPlaced("cust-1", DateTime.UtcNow),
        new OrderItemAdded("prod-1", 1, 9.99m, DateTime.UtcNow),
        new OrderShipped("FedEx", "TRACK123", DateTime.UtcNow)
    });

    var order = new Order(stream);
    await order.Fold();

    // When/Then: Adding items should throw
    await Assert.ThrowsAsync<InvalidOperationException>(
        () => order.AddItem("prod-2", 1, 19.99m));
}`;

  private readonly pattern1Code = `[Fact]
public async Task Ship_ShouldAppendShippedEvent()
{
    var context = TestSetup.GetContext();

    await AggregateTestBuilder.For<Order>("order-123", context)
        .Given(new OrderCreated("customer-1", 99.99m))
        .When(async order => await order.Ship("TRACK-001"))
        .Then(assertion =>
        {
            assertion.ShouldHaveAppended<OrderShipped>();
            assertion.ShouldHaveProperty(o => o.IsShipped, true);
        });
}

[Fact]
public async Task Cancel_ShouldThrow_WhenAlreadyShipped()
{
    var context = TestSetup.GetContext();

    var builderAfterWhen = await AggregateTestBuilder.For<Order>("order-456", context)
        .Given(
            new OrderCreated("customer-1", 100m),
            new OrderShipped("TRACK-001", DateTimeOffset.UtcNow))
        .When(async order => await order.Cancel("Changed mind"));

    var assertion = await builderAfterWhen.Then();
    assertion.ShouldThrow<InvalidOperationException>();
}`;

  private readonly pattern2Code = `[Fact]
public async Task CreateInvoice_ShouldAppendCreatedEvent()
{
    var context = TestSetup.GetContext();

    await AggregateTestBuilder<LegacyInvoice>.For(
        "invoice",
        "inv-001",
        context,
        stream => new LegacyInvoice(stream)
    )
    .GivenNoPriorEvents()
    .When(async invoice => await invoice.Create("customer-1", 500m))
    .Then(assertion =>
    {
        assertion.ShouldHaveAppended<InvoiceCreated>();
        assertion.ShouldHaveProperty(i => i.Amount, 500m);
    });
}`;

  private readonly projectionPattern1Code = `[Fact]
public async Task Should_aggregate_project_data()
{
    var context = TestSetup.GetContext();

    await ProjectionTestBuilder.For<ProjectDashboard>(context)
        .Given<Project>("project-1",
            new ProjectInitiated("Project A", "First project", "owner-1", DateTime.UtcNow),
            new MemberJoinedProject("member-1", "Developer", ReadWritePermissions, "owner-1", DateTime.UtcNow))
        .Given<Project>("project-2",
            new ProjectInitiated("Project B", "Second project", "owner-1", DateTime.UtcNow))
        .WhenProjectionUpdates()
        .Then(result => result
            .ShouldHaveState(p => p.TotalProjects == 2)
            .ShouldHaveState(p => p.TotalTeamMembers == 1));
}`;

  private readonly projectionPattern2Code = `[Fact]
public async Task Should_track_completed_projects()
{
    var context = TestSetup.GetContext();

    await ProjectionTestBuilder<LegacyDashboard>.Create(
        context,
        (docFactory, streamFactory) => new LegacyDashboard(docFactory, streamFactory)
    )
    .GivenEvents("Project", "project-1",
        new ProjectInitiated("Project A", "Description", "owner-1", DateTime.UtcNow),
        new ProjectCompletedSuccessfully("Done", "owner-1", DateTime.UtcNow))
    .GivenEvents("Project", "project-2",
        new ProjectInitiated("Project B", "Description", "owner-1", DateTime.UtcNow))
    .UpdateToLatest()
    .Then()
        .ShouldHaveState(p => p.TotalProjects == 2)
        .ShouldHaveState(p => p.CompletedProjects == 1);
}`;

  private readonly timeTestingCode = `using ErikLieben.FA.ES.Testing;
using ErikLieben.FA.ES.Testing.Builders;
using ErikLieben.FA.ES.Testing.Time;
using Xunit;

public class TimeBasedTests
{
    [Fact]
    public async Task Should_detect_overdue_work_item()
    {
        // TestClock extends TimeProvider - use it anywhere TimeProvider is expected
        var testClock = new TestClock();
        var context = TestSetup.GetContext(testClock);

        // Set initial time
        var startTime = new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero);
        testClock.SetUtcNow(startTime);

        // Given: A work item with a deadline 7 days from now
        var deadline = startTime.AddDays(7);

        await AggregateTestBuilder
            .For<WorkItem, WorkItemId>(WorkItemId.From(Guid.NewGuid()), context)
            .Given(
                new WorkItemOpened("Fix deadline bug", "user-1", startTime),
                new DeadlineSet(deadline, startTime))
            .When(async item =>
            {
                // Advance time past the deadline
                testClock.Advance(TimeSpan.FromDays(10));

                // Mark as overdue (uses TimeProvider internally)
                await item.CheckDeadline();
            })
            .Then(result => result
                .ShouldHaveAppended<WorkItemMarkedOverdue>()
                .ShouldHaveState(w => w.IsOverdue));
    }

    [Fact]
    public async Task Frozen_time_remains_constant()
    {
        var testClock = new TestClock();
        testClock.Freeze();

        var time1 = testClock.GetUtcNow();
        await Task.Delay(100);
        var time2 = testClock.GetUtcNow();

        Assert.Equal(time1, time2);

        testClock.Unfreeze();
    }
}`;

  private readonly performanceCode = `using ErikLieben.FA.ES.Testing;
using ErikLieben.FA.ES.Testing.Performance;
using TaskFlow.Domain.ValueObjects.Project;
using Xunit;

public class ProjectPerformanceTests
{
    private static MemberPermissions ReadOnlyPermissions =>
        new(CanEdit: false, CanDelete: false, CanInvite: false, CanManageWorkItems: false);

    [Fact]
    public async Task Folding_1000_events_should_complete_quickly()
    {
        var context = TestSetup.GetContext();
        var projectId = Guid.NewGuid().ToString();
        var stream = await context.GetEventStreamFor("Project", projectId);

        // Setup: Append 1000 events
        await stream.Session(ctx =>
        {
            ctx.Append(new ProjectInitiated("Perf Test", "Desc", "owner", DateTime.UtcNow));
            for (int i = 0; i < 999; i++)
            {
                ctx.Append(new MemberJoinedProject(
                    $"member-{i}",
                    "Developer",
                    ReadOnlyPermissions,
                    "owner",
                    DateTime.UtcNow));
            }
        });

        // Measure folding performance
        var metrics = await PerformanceMeasurement.MeasureAsync(async () =>
        {
            var project = new Project(stream);
            await project.Fold();
        });

        // Assert performance characteristics
        new PerformanceAssertion(metrics)
            .ShouldCompleteWithin(TimeSpan.FromMilliseconds(500))
            .ShouldNotCauseGen2Collection();

        // Log metrics for analysis
        Console.WriteLine($"Elapsed: {metrics.ElapsedTime.TotalMilliseconds}ms");
        Console.WriteLine($"Gen0 Collections: {metrics.Gen0Collections}");
        Console.WriteLine($"Gen1 Collections: {metrics.Gen1Collections}");
    }

    [Fact]
    public async Task Event_stream_operations_throughput()
    {
        var context = TestSetup.GetContext();

        var metrics = await PerformanceMeasurement.MeasureRepeatedAsync(
            iterations: 100,
            async () =>
            {
                var projectId = Guid.NewGuid().ToString();
                var stream = await context.GetEventStreamFor("Project", projectId);

                await stream.Session(ctx =>
                    ctx.Append(new ProjectInitiated("Test", "Desc", "owner", DateTime.UtcNow)));

                var project = new Project(stream);
                await project.Fold();
            });

        new PerformanceAssertion(metrics)
            .ShouldHaveThroughputOf(atLeast: 50);  // At least 50 ops/sec
    }
}`;

  private readonly completeExampleCode = `using ErikLieben.FA.ES.Testing;
using Xunit;

namespace MyApp.Tests;

public class OrderAggregateTests
{
    private readonly TestContext _context;

    public OrderAggregateTests()
    {
        _context = TestSetup.GetContext();
    }

    [Fact]
    public async Task Can_place_new_order()
    {
        // Arrange
        var stream = await _context.GetEventStreamFor("Order", "order-1");
        var order = new Order(stream);

        // Act
        await order.Place("customer-123");

        // Assert
        Assert.Equal("customer-123", order.CustomerId);
        Assert.Equal(OrderStatus.Placed, order.Status);

        _context.Assert
            .ShouldHaveObject("Order", "order-1")
            .WithEventCount(1);
    }

    [Fact]
    public async Task Can_add_item_to_order()
    {
        // Given
        var stream = await _context.GetEventStreamFor("Order", "order-1");
        await GivenOrderExists(stream, "customer-123");
        var order = await LoadOrder(stream);

        // When
        await order.AddItem("PROD-001", 2, 29.99m);

        // Then
        Assert.Single(order.Items);
        Assert.Equal(59.98m, order.TotalAmount);
    }

    [Fact]
    public async Task Can_ship_order_with_items()
    {
        // Given
        var stream = await _context.GetEventStreamFor("Order", "order-1");
        await GivenOrderWithItems(stream);
        var order = await LoadOrder(stream);

        // When
        await order.Ship("FedEx", "TRACK-12345");

        // Then
        Assert.Equal(OrderStatus.Shipped, order.Status);

        _context.Assert
            .ShouldHaveObject("Order", "order-1")
            .WithEventAtLastPosition(
                new OrderShipped("FedEx", "TRACK-12345", DateTime.UtcNow));
    }

    [Fact]
    public async Task Cannot_ship_empty_order()
    {
        // Given
        var stream = await _context.GetEventStreamFor("Order", "order-1");
        await GivenOrderExists(stream, "customer-123");
        var order = await LoadOrder(stream);

        // When/Then
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => order.Ship("FedEx", "TRACK-12345"));

        Assert.Contains("cannot ship empty order", ex.Message.ToLower());
    }

    [Fact]
    public async Task Cannot_add_items_after_shipping()
    {
        // Given
        var stream = await _context.GetEventStreamFor("Order", "order-1");
        await GivenShippedOrder(stream);
        var order = await LoadOrder(stream);

        // When/Then
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => order.AddItem("PROD-002", 1, 19.99m));
    }

    // Helper methods
    private async Task GivenOrderExists(IEventStream stream, string customerId)
    {
        await _context.AppendEvents(stream, new IEvent[]
        {
            new OrderPlaced(customerId, DateTime.UtcNow)
        });
    }

    private async Task GivenOrderWithItems(IEventStream stream)
    {
        await _context.AppendEvents(stream, new IEvent[]
        {
            new OrderPlaced("customer-123", DateTime.UtcNow),
            new OrderItemAdded("PROD-001", 2, 29.99m, DateTime.UtcNow)
        });
    }

    private async Task GivenShippedOrder(IEventStream stream)
    {
        await _context.AppendEvents(stream, new IEvent[]
        {
            new OrderPlaced("customer-123", DateTime.UtcNow),
            new OrderItemAdded("PROD-001", 1, 29.99m, DateTime.UtcNow),
            new OrderShipped("FedEx", "TRACK-123", DateTime.UtcNow)
        });
    }

    private async Task<Order> LoadOrder(IEventStream stream)
    {
        var order = new Order(stream);
        await order.Fold();
        return order;
    }
}`;

  constructor() {
    effect(() => {
      this.themeService.theme();
      this.highlightCodeSamples();
    });
  }

  ngOnInit(): void {
    this.highlightCodeSamples();
    this.setupIntersectionObserver();
  }

  ngOnDestroy(): void {
    if (this.intersectionObserver) {
      this.intersectionObserver.disconnect();
    }
  }

  private setupIntersectionObserver(): void {
    // Create observer that triggers when section enters viewport
    this.intersectionObserver = new IntersectionObserver(
      (entries) => {
        entries.forEach(entry => {
          const sectionId = entry.target.id;

          if (entry.isIntersecting) {
            this.visibleSections.add(sectionId);
          } else {
            this.visibleSections.delete(sectionId);
          }
        });

        this.updateActiveSection();
      },
      {
        // Trigger when any part of the section is visible
        threshold: 0,
        // Start detecting slightly before entering viewport
        rootMargin: '-80px 0px -50% 0px'
      }
    );

    // Observe all sections
    this.navItems.forEach(item => {
      const element = document.getElementById(item.id);
      if (element) {
        this.intersectionObserver!.observe(element);
      }
    });
  }

  private updateActiveSection(): void {
    // Find the first visible section in document order
    for (const item of this.navItems) {
      if (this.visibleSections.has(item.id)) {
        this.activeSection.set(item.id);
        return;
      }
    }

    // Fallback: if no sections visible, keep current or default to overview
    if (this.visibleSections.size === 0) {
      this.activeSection.set('overview');
    }
  }

  scrollToSection(event: Event, sectionId: string): void {
    event.preventDefault();
    const element = document.getElementById(sectionId);
    if (element) {
      element.scrollIntoView({ behavior: 'smooth', block: 'start' });
      this.activeSection.set(sectionId);
    }
  }

  private async highlightCodeSamples(): Promise<void> {
    const [
      install, setup, setupAlternative, pattern1, pattern2, aggregate, assertions,
      projection, projectionPattern1, projectionPattern2, timeTesting, given,
      invariant, performance, complete
    ] = await Promise.all([
      this.codeHighlighter.highlight(this.installCode, { language: 'bash' }),
      this.codeHighlighter.highlight(this.testSetupCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.testSetupAlternativeCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.pattern1Code, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.pattern2Code, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.aggregateTestCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.assertionsCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.projectionTestCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.projectionPattern1Code, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.projectionPattern2Code, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.timeTestingCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.givenEventsCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.invariantTestCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.performanceCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.completeExampleCode, { language: 'csharp' })
    ]);

    this.installCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(install));
    this.testSetupCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(setup));
    this.testSetupAlternativeCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(setupAlternative));
    this.pattern1CodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(pattern1));
    this.pattern2CodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(pattern2));
    this.aggregateTestCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(aggregate));
    this.assertionsCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(assertions));
    this.projectionTestCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(projection));
    this.projectionPattern1CodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(projectionPattern1));
    this.projectionPattern2CodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(projectionPattern2));
    this.timeTestingCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(timeTesting));
    this.givenEventsCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(given));
    this.invariantTestCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(invariant));
    this.performanceCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(performance));
    this.completeExampleCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(complete));
  }
}
