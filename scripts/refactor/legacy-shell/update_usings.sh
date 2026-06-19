#!/bin/bash

# Script to update using statements across the codebase

echo "Updating using statements for new folder structure..."

# Update all .cs files to add new using statements where needed
find WebHomestay -name "*.cs" -type f | while read file; do
    # Add using for new namespaces if file references moved types
    if grep -q "WebHomestay.Models" "$file" && ! grep -q "WebHomestay.Models.Entities" "$file"; then
        # Insert new using statements after existing WebHomestay.Models
        sed -i '/using WebHomestay.Models;/a\
using WebHomestay.Models.Entities.Core;\
using WebHomestay.Models.Entities.Slots;\
using WebHomestay.Models.Entities.Chat;\
using WebHomestay.Models.Entities.AI;\
using WebHomestay.Models.Enums;\
using WebHomestay.Models.DTOs.Booking;\
using WebHomestay.Models.DTOs.AI;\
using WebHomestay.Models.Configuration;\
using WebHomestay.Models.ViewModels;' "$file"

        # Remove the old generic using
        sed -i '/^using WebHomestay.Models;$/d' "$file"
    fi

    if grep -q "WebHomestay.Services\." "$file" && ! grep -q "WebHomestay.Services.Booking" "$file"; then
        sed -i '/using WebHomestay.Services;/a\
using WebHomestay.Services.Booking;\
using WebHomestay.Services.Slots;\
using WebHomestay.Services.Room;\
using WebHomestay.Services.Branch;\
using WebHomestay.Services.Chat;\
using WebHomestay.Services.Settings;\
using WebHomestay.Services.Infrastructure;' "$file"
    fi

    if grep -q "WebHomestay.Controllers\." "$file" && ! grep -q "WebHomestay.Controllers.Admin" "$file"; then
        sed -i '/using WebHomestay.Controllers;/a\
using WebHomestay.Controllers.Admin;\
using WebHomestay.Controllers.Public;\
using WebHomestay.Controllers.AI;' "$file"
    fi
done

echo "Done!"
