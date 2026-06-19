#!/bin/bash

# Fix all references to moved service implementations
find WebHomestay -name "*.cs" -type f -exec sed -i \
  -e 's/WebHomestay\.Services\.BookingCancellationService/WebHomestay.Services.Booking.BookingCancellationService/g' \
  -e 's/WebHomestay\.Services\.AvailabilityService/WebHomestay.Services.Booking.AvailabilityService/g' \
  -e 's/WebHomestay\.Services\.BookingCreationService/WebHomestay.Services.Booking.BookingCreationService/g' \
  -e 's/WebHomestay\.Services\.PricingService/WebHomestay.Services.Booking.PricingService/g' \
  -e 's/WebHomestay\.Services\.SlotGenerationService/WebHomestay.Services.Slots.SlotGenerationService/g' \
  -e 's/WebHomestay\.Services\.SlotManagementService/WebHomestay.Services.Slots.SlotManagementService/g' \
  -e 's/WebHomestay\.Services\.RoomBookingViewService/WebHomestay.Services.Room.RoomBookingViewService/g' \
  -e 's/WebHomestay\.Services\.PublicBookingRoomExplanationService/WebHomestay.Services.Room.PublicBookingRoomExplanationService/g' \
  -e 's/WebHomestay\.Services\.BranchLeadTimeService/WebHomestay.Services.Branch.BranchLeadTimeService/g' \
  -e 's/WebHomestay\.Services\.AdminChatService/WebHomestay.Services.Chat.AdminChatService/g' \
  -e 's/WebHomestay\.Services\.AdminChatQuickSendService/WebHomestay.Services.Chat.AdminChatQuickSendService/g' \
  -e 's/WebHomestay\.Services\.SettingService/WebHomestay.Services.Settings.SettingService/g' \
  -e 's/WebHomestay\.Services\.PaymentQrSettingsService/WebHomestay.Services.Settings.PaymentQrSettingsService/g' \
  -e 's/WebHomestay\.Services\.MailService/WebHomestay.Services.Infrastructure.MailService/g' \
  -e 's/WebHomestay\.Services\.BulkImportService/WebHomestay.Services.Infrastructure.BulkImportService/g' \
  -e 's/WebHomestay\.Services\.StatisticsService/WebHomestay.Services.Infrastructure.StatisticsService/g' \
  -e 's/WebHomestay\.Services\.ImageMaskingService/WebHomestay.Services.Infrastructure.ImageMaskingService/g' \
  {} \;

echo "Fixed all service namespace references"
