#!/bin/bash

# 1. Add using WebHomestay.Services; to all service implementation files in subfolders
for file in WebHomestay/Services/Booking/*.cs \
            WebHomestay/Services/Branch/*.cs \
            WebHomestay/Services/Room/*.cs \
            WebHomestay/Services/Slots/*.cs \
            WebHomestay/Services/Chat/*.cs \
            WebHomestay/Services/Settings/*.cs \
            WebHomestay/Services/Infrastructure/*.cs; do
    if [ -f "$file" ] && ! grep -q "using WebHomestay.Services;" "$file" 2>/dev/null; then
        sed -i '1i using WebHomestay.Services;' "$file"
    fi
done

# 2. Fix specific missing usings
files_needing_ai_dtos=(
  "WebHomestay/Services/Booking/BookingDecision.cs"
)

for file in "${files_needing_ai_dtos[@]}"; do
  if [ -f "$file" ] && ! grep -q "using WebHomestay.Models.DTOs.AI;" "$file" 2>/dev/null; then
    sed -i '1i using WebHomestay.Models.DTOs.AI;' "$file"
  fi
done

echo "Applied final fixes"
