# Design Spec: Admin Statistics Dashboard

## Overview
This document outlines the design and implementation of a comprehensive Statistics Dashboard for the Homestay Admin panel. The dashboard provides real-time insights into revenue, bookings, occupancy, and customer behavior.

## Goals
- Provide high-level business metrics at a glance.
- Allow deep-dive analysis per branch and per room.
- Enable data-driven decision-making using trend charts and peak-hour analysis.
- Ensure a premium, responsive, and fast user interface using AJAX.

## User Interface Design

### 1. Header & Filters
- **Title**: "Analytics Dashboard"
- **Quick Filters**: Today, This Week, This Month, This Year.
- **Custom Range**: Date range picker (Start Date - End Date).
- **Branch Selector**: Dropdown to filter statistics for a specific branch or "All Branches".

### 2. Summary Cards (Top Row)
- **Total Revenue**: Sum of `TotalPrice` for non-cancelled bookings.
- **Total Bookings**: Count of bookings in the selected range.
- **Occupancy Rate**: Percentage of time rooms were occupied vs. total availability.
- **Cancellation Rate**: % of cancelled bookings vs. total created bookings.

### 3. Data Visualization (Charts)
- **Revenue Trend (Line Chart)**: Daily/Monthly revenue over the selected period.
- **Booking Time Distribution (Bar Chart)**: Bookings grouped by the hour of `StartTime` (0-23h).
- **Booking Mode Ratio (Pie/Doughnut Chart)**: Comparison between Hourly and Daily bookings.

### 4. Detailed Performance Table (Bottom Section)
Columns:
- **Room/Branch Name**
- **Total Orders**
- **Total Revenue**
- **Occupancy %**
- **Avg. Stay Duration**

## Technical Architecture

### Backend (ASP.NET Core MVC)
- **Controller**: `StatisticsController`
- **Actions**:
    - `Index()`: Renders the main dashboard page.
    - `GetStats(DateTime? start, DateTime? end, int? branchId)`: Returns a JSON object containing all calculated metrics and chart data.
- **Service Layer**: A new `IStatisticsService` will be created to handle the complex LINQ queries and occupancy calculations.

### Frontend (JavaScript & CSS)
- **Library**: `Chart.js` for all visualizations.
- **Styling**: Premium custom CSS with a focus on dark mode aesthetics, glassmorphism, and smooth transitions.
- **AJAX**: Fetch data from `GetStats` when filters change and update charts using `chart.update()`.

## Data Models Involved
- `Booking`: Primary source for revenue and timing.
- `Room`: For capacity and pricing context.
- `Branch`: For geographic grouping.

## Success Criteria
- Dashboard loads in under 2 seconds.
- Charts accurately reflect database state.
- Filters correctly update all metrics without page reload.
