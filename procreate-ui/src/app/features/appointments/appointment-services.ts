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

export const PROCEDURE_SERVICES = ['Ultrasound', 'IUI', 'Semen Analysis'];

/**
 * The procedure each batch card stands for on the Procedures board, in the
 * order the cards are drawn. The blocks themselves are unchanged — this only
 * says which procedure a card is run for, so the desk reads the board by what
 * is being done rather than by the clock.
 */
export const PROCEDURE_BATCH_CARDS = ['IUI', 'Semen Analysis', 'Ultrasound'];
