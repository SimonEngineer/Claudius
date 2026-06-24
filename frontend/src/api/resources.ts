import { api } from './client'
import type {
  Tag, WishlistLocation, WishlistNote, WishlistLink, WishlistPlan, Whiteboard,
  Goal, GoalFieldDefinition, GoalItem, GoalItemFieldValue,
  Trip, TripStop, Booking, TimelineEntry, MediaItem, EntityTypeValue, GoalFieldTypeValue,
  PackingItem, TripLink,
} from '@/types'

// Tags
export const TagsApi = {
  list: () => api.get<Tag[]>('/tags').then((r) => r.data),
  create: (name: string, color = '#64748b') => api.post<Tag>('/tags', { name, color }).then((r) => r.data),
  assign: (tagId: string, entityType: EntityTypeValue, entityId: string) =>
    api.post(`/tags/${tagId}/assign`, { entityType, entityId }),
  unassign: (tagId: string, entityType: EntityTypeValue, entityId: string) =>
    api.post(`/tags/${tagId}/unassign`, { entityType, entityId }),
  remove: (id: string) => api.delete(`/tags/${id}`),
}

// Media
export const MediaApi = {
  list: (entityType: EntityTypeValue, entityId: string) =>
    api.get<MediaItem[]>('/media', { params: { entityType, entityId } }).then((r) => r.data),
  create: (data: Partial<MediaItem> & { entityType: EntityTypeValue; entityId: string; kind: number; url: string }) =>
    api.post<MediaItem>('/media', data).then((r) => r.data),
  upload: (file: File) => {
    const form = new FormData()
    form.append('file', file)
    return api.post<{ url: string }>('/media/upload', form, { headers: { 'Content-Type': 'multipart/form-data' } }).then((r) => r.data)
  },
  remove: (id: string) => api.delete(`/media/${id}`),
}

// Locations (wishlist)
export const LocationsApi = {
  list: () => api.get<WishlistLocation[]>('/locations').then((r) => r.data),
  get: (id: string) => api.get<WishlistLocation>(`/locations/${id}`).then((r) => r.data),
  create: (data: Partial<WishlistLocation>) => api.post<WishlistLocation>('/locations', data).then((r) => r.data),
  update: (id: string, data: Partial<WishlistLocation>) => api.put<WishlistLocation>(`/locations/${id}`, data).then((r) => r.data),
  remove: (id: string) => api.delete(`/locations/${id}`),

  notes: (id: string) => api.get<WishlistNote[]>(`/locations/${id}/notes`).then((r) => r.data),
  addNote: (id: string, text: string) => api.post<WishlistNote>(`/locations/${id}/notes`, { text }).then((r) => r.data),
  removeNote: (noteId: string) => api.delete(`/locations/notes/${noteId}`),

  links: (id: string) => api.get<WishlistLink[]>(`/locations/${id}/links`).then((r) => r.data),
  addLink: (id: string, url: string, label?: string) => api.post<WishlistLink>(`/locations/${id}/links`, { url, label }).then((r) => r.data),
  removeLink: (linkId: string) => api.delete(`/locations/links/${linkId}`),

  getPlan: (id: string) => api.get<WishlistPlan>(`/locations/${id}/plan`).then((r) => r.data),
  putPlan: (id: string, data: WishlistPlan) => api.put<WishlistPlan>(`/locations/${id}/plan`, data).then((r) => r.data),

  getWhiteboard: (id: string) => api.get<Whiteboard>(`/locations/${id}/whiteboard`).then((r) => r.data),
  putWhiteboard: (id: string, data: Whiteboard) => api.put<Whiteboard>(`/locations/${id}/whiteboard`, data).then((r) => r.data),

  tripLinks: (id: string) => api.get<TripLink[]>(`/locations/${id}/trip-links`).then((r) => r.data),
}

// Goals
export const GoalsApi = {
  list: () => api.get<Goal[]>('/goals').then((r) => r.data),
  get: (id: string) => api.get<Goal>(`/goals/${id}`).then((r) => r.data),
  create: (data: { name: string; description?: string; icon?: string; kind: number; linkedTripId?: string | null; fieldDefinitions: Omit<GoalFieldDefinition, 'id'>[] }) =>
    api.post<Goal>('/goals', data).then((r) => r.data),
  update: (id: string, data: { name: string; description?: string; icon?: string; linkedTripId?: string | null }) =>
    api.put<Goal>(`/goals/${id}`, data).then((r) => r.data),
  remove: (id: string) => api.delete(`/goals/${id}`),
  addField: (id: string, data: { key: string; label: string; fieldType: GoalFieldTypeValue; sortOrder: number }) =>
    api.post<GoalFieldDefinition>(`/goals/${id}/fields`, data).then((r) => r.data),
  removeField: (fieldId: string) => api.delete(`/goals/fields/${fieldId}`),

  items: (id: string) => api.get<GoalItem[]>(`/goals/${id}/items`).then((r) => r.data),
  addItem: (id: string, data: { name: string; lat?: number | null; lng?: number | null; fieldValues: GoalItemFieldValue[] }) =>
    api.post<GoalItem>(`/goals/${id}/items`, data).then((r) => r.data),
  updateItem: (itemId: string, data: { name: string; lat?: number | null; lng?: number | null; fieldValues: GoalItemFieldValue[] }) =>
    api.put<GoalItem>(`/goals/items/${itemId}`, data).then((r) => r.data),
  completeItem: (itemId: string, completed: boolean) =>
    api.post<GoalItem>(`/goals/items/${itemId}/complete?completed=${completed}`).then((r) => r.data),
  removeItem: (itemId: string) => api.delete(`/goals/items/${itemId}`),
  addItemNote: (itemId: string, text: string) => api.post(`/goals/items/${itemId}/notes`, { text }).then((r) => r.data),
  addItemLink: (itemId: string, url: string, label?: string) => api.post(`/goals/items/${itemId}/links`, { url, label }).then((r) => r.data),
}

// Trips
export const TripsApi = {
  list: () => api.get<Trip[]>('/trips').then((r) => r.data),
  get: (id: string) => api.get<Trip>(`/trips/${id}`).then((r) => r.data),
  create: (data: Partial<Trip>) => api.post<Trip>('/trips', data).then((r) => r.data),
  update: (id: string, data: Partial<Trip>) => api.put<Trip>(`/trips/${id}`, data).then((r) => r.data),
  remove: (id: string) => api.delete(`/trips/${id}`),

  stops: (id: string) => api.get<TripStop[]>(`/trips/${id}/stops`).then((r) => r.data),
  addStop: (id: string, data: Omit<TripStop, 'id'>) => api.post<TripStop>(`/trips/${id}/stops`, data).then((r) => r.data),
  updateStop: (stopId: string, data: Omit<TripStop, 'id'>) => api.put<TripStop>(`/trips/stops/${stopId}`, data).then((r) => r.data),
  removeStop: (stopId: string) => api.delete(`/trips/stops/${stopId}`),
  reorderStops: (id: string, orderedStopIds: string[]) => api.put(`/trips/${id}/stops/reorder`, orderedStopIds),

  bookings: (id: string) => api.get<Booking[]>(`/trips/${id}/bookings`).then((r) => r.data),
  addBooking: (id: string, data: Omit<Booking, 'id'>) => api.post<Booking>(`/trips/${id}/bookings`, data).then((r) => r.data),
  updateBooking: (bookingId: string, data: Omit<Booking, 'id'>) => api.put<Booking>(`/trips/bookings/${bookingId}`, data).then((r) => r.data),
  removeBooking: (bookingId: string) => api.delete(`/trips/bookings/${bookingId}`),

  timeline: (id: string) => api.get<TimelineEntry[]>(`/trips/${id}/timeline`).then((r) => r.data),
  addTimelineEntry: (id: string, data: { type: number; content?: string; lat?: number | null; lng?: number | null; capturedAt?: string | null }) =>
    api.post<TimelineEntry>(`/trips/${id}/timeline`, data).then((r) => r.data),
  removeTimelineEntry: (entryId: string) => api.delete(`/trips/timeline/${entryId}`),

  packing: (id: string) => api.get<PackingItem[]>(`/trips/${id}/packing`).then((r) => r.data),
  addPackingItem: (id: string, name: string) => api.post<PackingItem>(`/trips/${id}/packing`, { name }).then((r) => r.data),
  togglePackingItem: (itemId: string, packed: boolean) => api.post<PackingItem>(`/trips/packing/${itemId}/toggle?packed=${packed}`).then((r) => r.data),
  removePackingItem: (itemId: string) => api.delete(`/trips/packing/${itemId}`),
}
