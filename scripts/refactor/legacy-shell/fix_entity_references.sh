#!/bin/bash

# Fix entity type references when they appear with WebHomestay.Services prefix
find WebHomestay -name "*.cs" -type f -exec sed -i \
  -e 's/WebHomestay\.Services\.SystemSetting/WebHomestay.Models.Entities.Core.SystemSetting/g' \
  -e 's/WebHomestay\.Services\.Branch\b/WebHomestay.Models.Entities.Core.Branch/g' \
  -e 's/WebHomestay\.Services\.Booking\b/WebHomestay.Models.Entities.Core.Booking/g' \
  -e 's/WebHomestay\.Services\.Holiday/WebHomestay.Models.Entities.Core.Holiday/g' \
  -e 's/WebHomestay\.Services\.RoomSlotTemplate/WebHomestay.Models.Entities.Slots.RoomSlotTemplate/g' \
  -e 's/WebHomestay\.Services\.AdminRole/WebHomestay.Models.Entities.Core.AdminRole/g' \
  {} \;

echo "Fixed entity type references"
