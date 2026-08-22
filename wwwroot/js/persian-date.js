/**
 * persian-date.js — lightweight Jalali (Persian) date library
 *
 * API-compatible drop-in replacement. Pure math, no dependencies.
 *
 * Usage:
 *   var d = new persianDate();
 *   var d = new persianDate([1403, 5, 29]);
 *   d.year()  → 1403
 *   d.month() → 5   (1-based)
 *   d.date()  → 29
 *   d.format('YYYY/MM/DD') → "1403/05/29"
 *   d.toDate() → JavaScript Date
 *   d.daysInMonth(1403, 5) → 31
 *   d.getFirstWeekDayOfMonth(1403, 5) → 0..6 (Sat..Fri)
 */
(function (global) {
  'use strict';

  var PERSIAN_EPOCH = 1948320;

  function jalaliToJd(year, month, day) {
    var epbase = year - (year >= 0 ? 474 : 473);
    var epyear = 474 + (epbase % 2820);
    return day +
      ((month <= 7) ? (month - 1) * 31 : (month - 1) * 30 + 6) +
      Math.floor((epyear * 682 - 110) / 2816) +
      (epyear - 1) * 365 +
      Math.floor(epbase / 2820) * 1029983 +
      (PERSIAN_EPOCH - 1);
  }

  function jdToJalali(jd) {
    var depoch = jd - jalaliToJd(475, 1, 1);
    var cycle = Math.floor(depoch / 1029983);
    var cyear = depoch % 1029983;
    var ycycle;
    if (cyear === 1029982) {
      ycycle = 2820;
    } else {
      var aux1 = Math.floor(cyear / 366);
      var aux2 = cyear % 366;
      ycycle = Math.floor((2134 * aux1 + 2816 * aux2 + 2816) / 1028522) + aux1 + 1;
    }
    var year = ycycle + 2820 * cycle + 474;
    if (year <= 0) year--;
    var yday = jd - jalaliToJd(year, 1, 1) + 1;
    var month = (yday <= 186) ? Math.ceil(yday / 31) : Math.ceil((yday - 6) / 30);
    var day = jd - jalaliToJd(year, month, 1) + 1;
    return [year, month, day];
  }

  function gregorianToJd(year, month, day) {
    var a = Math.floor((14 - month) / 12);
    var y = year + 4800 - a;
    var m = month + 12 * a - 3;
    return day + Math.floor((153 * m + 2) / 5) + 365 * y +
      Math.floor(y / 4) - Math.floor(y / 100) + Math.floor(y / 400) - 32045;
  }

  function jdToGregorian(jd) {
    var a = jd + 32044;
    var b = Math.floor((4 * a + 3) / 146097);
    var c = a - Math.floor(146097 * b / 4);
    var d = Math.floor((4 * c + 3) / 1461);
    var e = c - Math.floor(1461 * d / 4);
    var m = Math.floor((5 * e + 2) / 153);
    var day = e - Math.floor((153 * m + 2) / 5) + 1;
    var month = m + 3 - 12 * Math.floor(m / 10);
    var year = 100 * b + d - 4800 + Math.floor(m / 10);
    return [year, month, day];
  }

  function isLeapJalaliYear(year) {
    return jalaliToJd(year + 1, 1, 1) - jalaliToJd(year, 1, 1) > 365;
  }

  function daysInMonth(year, month) {
    if (month <= 6) return 31;
    if (month <= 11) return 30;
    return isLeapJalaliYear(year) ? 30 : 29;
  }

  function getFirstWeekDayOfMonth(year, month) {
    var jd = jalaliToJd(year, month, 1);
    return (jd + 2) % 7;
  }

  function persianDate(input) {
    if (!(this instanceof persianDate)) {
      return new persianDate(input);
    }
    var gy, gm, gd, py, pm, pd, jd;
    if (input === undefined || input === null) {
      var now = new Date();
      gy = now.getFullYear(); gm = now.getMonth() + 1; gd = now.getDate();
    } else if (Array.isArray(input) && input.length >= 3) {
      py = input[0]; pm = input[1]; pd = input[2];
      jd = jalaliToJd(py, pm, pd);
      var g = jdToGregorian(jd);
      gy = g[0]; gm = g[1]; gd = g[2];
    } else if (input instanceof Date) {
      gy = input.getFullYear(); gm = input.getMonth() + 1; gd = input.getDate();
    } else {
      var now = new Date();
      gy = now.getFullYear(); gm = now.getMonth() + 1; gd = now.getDate();
    }
    if (!py) {
      jd = gregorianToJd(gy, gm, gd);
      var j = jdToJalali(jd);
      py = j[0]; pm = j[1]; pd = j[2];
    }
    this._gy = gy; this._gm = gm; this._gd = gd;
    this._py = py; this._pm = pm; this._pd = pd;
    this._jd = jd;
  }

  persianDate.prototype = {
    constructor: persianDate,
    year: function () { return this._py; },
    month: function () { return this._pm; },
    date: function () { return this._pd; },
    format: function (pattern) {
      if (!pattern) pattern = 'YYYY/MM/DD';
      return pattern
        .replace('YYYY', String(this._py))
        .replace('MM', String(this._pm).padStart(2, '0'))
        .replace('DD', String(this._pd).padStart(2, '0'));
    },
    toDate: function () { return new Date(this._gy, this._gm - 1, this._gd); },
    daysInMonth: function (y, m) {
      if (y === undefined) y = this._py;
      if (m === undefined) m = this._pm;
      return daysInMonth(y, m);
    },
    getFirstWeekDayOfMonth: function (y, m) {
      if (y === undefined) y = this._py;
      if (m === undefined) m = this._pm;
      return getFirstWeekDayOfMonth(y, m);
    }
  };

  persianDate.daysInMonth = daysInMonth;
  persianDate.getFirstWeekDayOfMonth = getFirstWeekDayOfMonth;
  global.persianDate = persianDate;
})(typeof window !== 'undefined' ? window : this);
