import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import axios from "../../services/axiosClient";
import type { Appointment, AppointmentType } from "./api";

export interface BookingType extends AppointmentType {
  bufferBeforeMinutes: number; bufferAfterMinutes: number; minBookingHours: number; maxBookingDays: number;
  maxAttendees: number; freeCancellationHours: number; meetingMode: string; location?: string; breakStart?: string; breakEnd?: string;
}
export interface Booking extends Omit<Appointment, "status"> {
  status: Appointment["status"] | "RescheduleProposed" | "CheckedIn";
  proposedStartAt?: string; proposedById?: string; rescheduleCount: number; refundDue: number; refundedAt?: string;
  depositReportedAt?: string; location?: string; leadId?: string; depositHoldUntil?: string; freeCancellationHours: number;
}
export interface DayException { id: string; appointmentTypeId: string; date: string; isClosed: boolean; openTime?: string; closeTime?: string; reason?: string }
export const useMyBookings = () => useQuery({ queryKey: ["my-appointments"], queryFn: async () => (await axios.get<Booking[]>("/api/appointments/my")).data });
export const useVendorBookings = () => useQuery({ queryKey: ["vendor-appointments"], queryFn: async () => (await axios.get<Booking[]>("/api/appointments/vendor")).data });
export const useBookingTypes = () => useQuery({ queryKey: ["my-appt-types"], queryFn: async () => (await axios.get<BookingType[]>("/api/appointments/vendor/types")).data });
export const useDayExceptions = (typeId?: string) => useQuery({ queryKey: ["appt-exceptions", typeId], enabled: !!typeId, queryFn: async () => (await axios.get<DayException[]>(`/api/appointments/vendor/types/${typeId}/exceptions`)).data });
export function useSaveException() {
  const qc = useQueryClient();
  return useMutation({ mutationFn: (b: { typeId: string; date: string; isClosed: boolean; openTime?: string; closeTime?: string; reason?: string; delete?: boolean }) => axios.post("/api/appointments/vendor/exceptions", b), onSuccess: () => qc.invalidateQueries() });
}
export async function downloadAppointmentCalendar(id: string) {
  const result = await axios.get(`/api/appointments/${id}.ics`, { responseType: "blob" });
  const url = URL.createObjectURL(result.data); const link = document.createElement("a");
  link.href = url; link.download = `wedding-match-${id}.ics`; link.click(); URL.revokeObjectURL(url);
}
