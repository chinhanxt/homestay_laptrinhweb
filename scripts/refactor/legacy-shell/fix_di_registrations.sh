#!/bin/bash

# Update DI registrations in Program.cs
sed -i \
  -e 's/WebHomestay\.Services\.IAvailabilityService, WebHomestay\.Services\.AvailabilityService/WebHomestay.Services.IAvailabilityService, WebHomestay.Services.Booking.AvailabilityService/g' \
  -e 's/WebHomestay\.Services\.IBulkImportService, WebHomestay\.Services\.BulkImportService/WebHomestay.Services.IBulkImportService, WebHomestay.Services.Infrastructure.BulkImportService/g' \
  -e 's/WebHomestay\.Services\.ISlotGenerationService, WebHomestay\.Services\.SlotGenerationService/WebHomestay.Services.ISlotGenerationService, WebHomestay.Services.Slots.SlotGenerationService/g' \
  -e 's/WebHomestay\.Services\.IRoomBookingViewService, WebHomestay\.Services\.RoomBookingViewService/WebHomestay.Services.IRoomBookingViewService, WebHomestay.Services.Room.RoomBookingViewService/g' \
  -e 's/WebHomestay\.Services\.IBookingCreationService, WebHomestay\.Services\.BookingCreationService/WebHomestay.Services.IBookingCreationService, WebHomestay.Services.Booking.BookingCreationService/g' \
  -e 's/WebHomestay\.Services\.IBookingCancellationService, WebHomestay\.Services\.BookingCancellationService/WebHomestay.Services.IBookingCancellationService, WebHomestay.Services.Booking.BookingCancellationService/g' \
  -e 's/WebHomestay\.Services\.ISlotManagementService, WebHomestay\.Services\.SlotManagementService/WebHomestay.Services.ISlotManagementService, WebHomestay.Services.Slots.SlotManagementService/g' \
  -e 's/WebHomestay\.Services\.IMailService, WebHomestay\.Services\.MailService/WebHomestay.Services.IMailService, WebHomestay.Services.Infrastructure.MailService/g' \
  -e 's/WebHomestay\.Services\.IBranchLeadTimeService, WebHomestay\.Services\.BranchLeadTimeService/WebHomestay.Services.IBranchLeadTimeService, WebHomestay.Services.Branch.BranchLeadTimeService/g' \
  -e 's/WebHomestay\.Services\.ISettingService, WebHomestay\.Services\.SettingService/WebHomestay.Services.ISettingService, WebHomestay.Services.Settings.SettingService/g' \
  -e 's/WebHomestay\.Services\.IStatisticsService, WebHomestay\.Services\.StatisticsService/WebHomestay.Services.IStatisticsService, WebHomestay.Services.Infrastructure.StatisticsService/g' \
  -e 's/WebHomestay\.Services\.IImageMaskingService, WebHomestay\.Services\.ImageMaskingService/WebHomestay.Services.IImageMaskingService, WebHomestay.Services.Infrastructure.ImageMaskingService/g' \
  -e 's/WebHomestay\.Services\.IPaymentQrSettingsService, WebHomestay\.Services\.PaymentQrSettingsService/WebHomestay.Services.IPaymentQrSettingsService, WebHomestay.Services.Settings.PaymentQrSettingsService/g' \
  -e 's/WebHomestay\.Services\.IPublicBookingRoomExplanationService, WebHomestay\.Services\.PublicBookingRoomExplanationService/WebHomestay.Services.IPublicBookingRoomExplanationService, WebHomestay.Services.Room.PublicBookingRoomExplanationService/g' \
  -e 's/WebHomestay\.Services\.IAdminChatService, WebHomestay\.Services\.AdminChatService/WebHomestay.Services.IAdminChatService, WebHomestay.Services.Chat.AdminChatService/g' \
  -e 's/WebHomestay\.Services\.IAdminChatQuickSendService, WebHomestay\.Services\.AdminChatQuickSendService/WebHomestay.Services.IAdminChatQuickSendService, WebHomestay.Services.Chat.AdminChatQuickSendService/g' \
  WebHomestay/Program.cs

echo "Updated DI registrations in Program.cs"
