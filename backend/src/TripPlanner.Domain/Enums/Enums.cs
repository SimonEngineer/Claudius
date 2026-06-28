namespace TripPlanner.Domain.Enums;

public enum EntityType
{
    WishlistLocation = 1,
    GoalItem = 2,
    Trip = 3,
    TripStop = 4,
    Booking = 5,
    TimelineEntry = 6,
    Goal = 7,
}

public enum MediaKind
{
    Photo = 1,
    Video = 2,
}

public enum GoalKind
{
    Checklist = 1,
    TripLink = 2,
}

public enum GoalFieldType
{
    Text = 1,
    Number = 2,
    Date = 3,
    Url = 4,
    Boolean = 5,
    Location = 6,
}

public enum TripStatus
{
    Planning = 1,
    Active = 2,
    Completed = 3,
}

public enum BookingType
{
    Flight = 1,
    Hotel = 2,
    CarRental = 3,
    Ticket = 4,
    Other = 5,
}

public enum TimelineEntryType
{
    Note = 1,
    Photo = 2,
    Video = 3,
    Url = 4,
}

public enum ExpenseCategory
{
    Lodging = 1,
    Transport = 2,
    Food = 3,
    Activities = 4,
    Other = 5,
}

public enum WishlistStatus
{
    Idea = 1,
    Planned = 2,
    Booked = 3,
    Visited = 4,
}

public enum DocumentType
{
    Passport = 1,
    Visa = 2,
    Insurance = 3,
    BookingConfirmation = 4,
    Other = 5,
}
