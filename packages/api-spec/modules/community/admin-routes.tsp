import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Community;

@useAuth(BearerAuth)
@route("/admin/community")
interface AdminCommunityOperations {
  // --- Plans ---
  @get
  @route("/plans")
  getPlans(): CommunityPlanDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/plans/{id}")
  getPlanById(
    @path id: string,
  ): CommunityPlanDto | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/plans")
  createPlan(
    @body body: CreatePlanRequestDto,
  ): LazuarApi.Core.IdResponse | LazuarApi.Core.ProblemDetailsResponse;

  @put
  @route("/plans/{id}")
  updatePlan(
    @path id: string,
    @body body: UpdatePlanRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @delete
  @route("/plans/{id}")
  archivePlan(
    @path id: string,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  // --- Subscribers ---
  @get
  @route("/subscribers")
  getSubscribers(
  ): CommunitySubscriptionDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/subscribers/export")
  exportSubscribers(): {
    @header contentType: "text/csv";
    @body content: bytes;
  } | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/subscribers/{id}/payments")
  getPaymentHistory(
    @path id: string,
  ): PaymentRecordDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/subscribers/{id}/reminders")
  getReminderHistory(
    @path id: string,
  ): DeliveryHistoryItemDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/subscribers")
  createSubscriber(
    @body body: CreateSubscriberRequestDto,
  ): LazuarApi.Core.IdResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/subscribers/{id}/payments")
  recordPaymentViaPayments(
    @path id: string,
    @body body: RecordPaymentRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/subscribers/{id}/record-payment")
  recordPayment(
    @path id: string,
    @body body: RecordPaymentRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/subscribers/{id}/cancel")
  cancelSubscription(
    @path id: string,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @put
  @route("/subscribers/{id}")
  updateSubscriberProfile(
    @path id: string,
    @body body: UpdateSubscriberProfileRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/subscribers/{id}/extend-grace")
  extendGrace(
    @path id: string,
    @body body: ExtendGraceRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/subscribers/{id}/pause-reminders")
  pauseReminders(
    @path id: string,
    @body body: PauseRemindersRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/subscribers/{id}/send-reminder")
  sendReminder(
    @path id: string,
    @body body: SendOneOffReminderRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/reminders/schedule-one-off")
  scheduleOneOffReminder(
    @body body: ScheduleOneOffRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  // --- Reminder Schedules ---
  @get
  @route("/reminder-schedules")
  getReminderSchedules(
  ): CommunityReminderScheduleDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/reminder-schedules")
  createReminderSchedule(
    @body body: CreateReminderScheduleRequestDto,
  ): LazuarApi.Core.IdResponse | LazuarApi.Core.ProblemDetailsResponse;

  @put
  @route("/reminder-schedules/{id}")
  updateReminderSchedule(
    @path id: string,
    @body body: UpdateReminderScheduleRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @delete
  @route("/reminder-schedules/{id}")
  deleteReminderSchedule(
    @path id: string,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  // --- Settings & Stats ---
  @get
  @route("/stats")
  getStats(
  ): CommunitySubscriberStatsDto | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/payment-config")
  getPaymentConfig(): PaymentConfigDto | LazuarApi.Core.ProblemDetailsResponse;

  @put
  @route("/payment-config")
  savePaymentConfig(
    @body body: SavePaymentConfigRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  // --- Templates (Localized to Community) ---
  @get
  @route("/templates")
  getTemplates(): MessageTemplateDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @put
  @route("/templates/{id}")
  updateTemplate(
    @path id: string,
    @body body: UpdateTemplateRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @delete
  @route("/templates/{id}")
  resetTemplate(
    @path id: string,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/reminders/test")
  testReminder(
    @body body: TestReminderRequestDto,
  ): TestReminderResponse | LazuarApi.Core.ProblemDetailsResponse;
}
