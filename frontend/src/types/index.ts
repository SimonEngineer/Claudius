export interface Tag {
  id: string
  name: string
  color: string
}

export const EntityType = {
  WishlistLocation: 1,
  GoalItem: 2,
  Trip: 3,
  TripStop: 4,
  Booking: 5,
  TimelineEntry: 6,
  Goal: 7,
} as const
export type EntityTypeValue = (typeof EntityType)[keyof typeof EntityType]

export const MediaKind = { Photo: 1, Video: 2 } as const

export interface MediaItem {
  id: string
  entityType: number
  entityId: string
  kind: number
  url: string
  caption?: string | null
  capturedAt?: string | null
  lat?: number | null
  lng?: number | null
  createdAt: string
}

export interface WishlistLocation {
  id: string
  name: string
  description?: string | null
  country?: string | null
  lat?: number | null
  lng?: number | null
  createdAt: string
  tags: Tag[]
}

export interface WishlistNote {
  id: string
  text: string
  createdAt: string
}

export interface WishlistLink {
  id: string
  url: string
  label?: string | null
}

export interface WishlistPlan {
  transportDetails?: string | null
  accommodationDetails?: string | null
  dateRangeStart?: string | null
  dateRangeEnd?: string | null
  freeformText?: string | null
}

export interface Whiteboard {
  contentJson: string
}

export const GoalKind = { Checklist: 1, TripLink: 2 } as const

export const GoalFieldType = {
  Text: 1,
  Number: 2,
  Date: 3,
  Url: 4,
  Boolean: 5,
  Location: 6,
} as const
export type GoalFieldTypeValue = (typeof GoalFieldType)[keyof typeof GoalFieldType]

export interface GoalFieldDefinition {
  id: string
  key: string
  label: string
  fieldType: GoalFieldTypeValue
  sortOrder: number
}

export interface Goal {
  id: string
  name: string
  description?: string | null
  icon?: string | null
  kind: number
  linkedTripId?: string | null
  fieldDefinitions: GoalFieldDefinition[]
  itemCount: number
  completedCount: number
  tags: Tag[]
}

export interface GoalItemFieldValue {
  goalFieldDefinitionId: string
  value?: string | null
}

export interface GoalItemNote {
  id: string
  text: string
  createdAt: string
}

export interface GoalItemLink {
  id: string
  url: string
  label?: string | null
}

export interface GoalItem {
  id: string
  goalId: string
  name: string
  lat?: number | null
  lng?: number | null
  isCompleted: boolean
  completedAt?: string | null
  fieldValues: GoalItemFieldValue[]
  notes: GoalItemNote[]
  links: GoalItemLink[]
  tags: Tag[]
}

export const TripStatus = { Planning: 1, Active: 2, Completed: 3 } as const

export interface Trip {
  id: string
  name: string
  description?: string | null
  startDate?: string | null
  endDate?: string | null
  status: number
  tags: Tag[]
}

export interface TripStop {
  id: string
  name: string
  lat: number
  lng: number
  arriveDate?: string | null
  departDate?: string | null
  sortOrder: number
  isStart: boolean
  isEnd: boolean
  notes?: string | null
  sourceLocationId?: string | null
}

export const BookingType = { Flight: 1, Hotel: 2, CarRental: 3, Ticket: 4, Other: 5 } as const

export interface Booking {
  id: string
  type: number
  title: string
  confirmationNumber?: string | null
  startAt?: string | null
  endAt?: string | null
  lat?: number | null
  lng?: number | null
  detailsJson?: string | null
  cost?: number | null
}

export interface PackingItem {
  id: string
  name: string
  isPacked: boolean
}

export interface TripLink {
  tripId: string
  tripName: string
  stopId: string
}

export const TimelineEntryType = { Note: 1, Photo: 2, Video: 3, Url: 4 } as const

export interface TimelineEntry {
  id: string
  type: number
  content?: string | null
  lat?: number | null
  lng?: number | null
  capturedAt: string
  nearestStopId?: string | null
}
