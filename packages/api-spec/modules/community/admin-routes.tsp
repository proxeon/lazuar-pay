import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";
using TypeSpec.Http;

namespace LazuarApi.Community;

@useAuth(BearerAuth)
@route("/admin/community")
interface AdminCommunityOperations {
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

  @get
  @route("/subscribers")
  getSubscribers(
    @query page?: int32 = 1,
    @query limit?: int32 = 50,
  ): LazuarApi.Core.PaginatedResponse<CommunitySubscriptionDto> | LazuarApi.Core.ProblemDetailsResponse;

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
    @query page?: int32 = 1,
    @query limit?: int32 = 50,
  ): LazuarApi.Core.PaginatedResponse<PaymentRecordDto> | LazuarApi.Core.ProblemDetailsResponse;

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

  @post
  @route("/subscribers/{id}/refund")
  refundPayment(
    @path id: string,
    @body body: RefundRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/subscribers/{id}/change-plan")
  changePlan(
    @path id: string,
    @body body: ChangePlanRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/subscribers/{id}/ban")
  banSubscriber(
    @path id: string,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/subscribers/{id}/resend-onboarding")
  resendOnboarding(
    @path id: string,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

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

  @get
  @route("/coupons")
  getCoupons(): CouponDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/coupons")
  createCoupon(
    @body body: CreateCouponRequestDto,
  ): LazuarApi.Core.IdResponse | LazuarApi.Core.ProblemDetailsResponse;
}
