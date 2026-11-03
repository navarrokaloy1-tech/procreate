/**
 * The services each calendar books, shared so the booking sheet and the batch
 * editor stay in agreement about what a "procedure" is. Keep these two lists in
 * sync with how the API splits appointments (see appointmentsController / the
 * calendar's procedureServiceSet).
 */
export const CONSULTATION_SERVICES = [
  'General Consultation',
  'Follow-up Consultation',
  'Prenatal Check-up',
  'Fertility Consultation',
  'Laboratory Request',
];

export const PROCEDURE_SERVICES = ['Ultrasound', 'IUI', 'Procedure'];
