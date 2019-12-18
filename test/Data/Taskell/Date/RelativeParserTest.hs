{-# LANGUAGE NoImplicitPrelude #-}
{-# LANGUAGE OverloadedStrings #-}

module Data.Taskell.Date.RelativeParserTest
    ( test_relative_parser
    ) where

import ClassyPrelude

import Test.Tasty
import Test.Tasty.HUnit

import Data.Time.Calendar (fromGregorian)
import Data.Time.Clock    (secondsToDiffTime)

import Data.Taskell.Date.RelativeParser (parseDate)

toTime :: (Integer, Int, Int) -> Integer -> UTCTime
toTime (y, m, d) seconds = UTCTime (fromGregorian y m d) (secondsToDiffTime seconds)

-- 08:53:03 18th December 2019
time :: UTCTime
time = toTime (2019, 12, 18) 31983

-- tests
test_relative_parser :: TestTree
test_relative_parser =
    testGroup
        "Data.Taskell.Date.RelativeParser"
        [ testCase
              "yyyy-mm-dd"
              (assertEqual
                   "Date from yyyy-mm-dd format"
                   (Right (toTime (2019, 12, 18) 0))
                   (parseDate time "2019-12-18"))
        , testCase
              "Second"
              (assertEqual
                   "Adds a second"
                   (Right (toTime (2019, 12, 18) 31984))
                   (parseDate time "1s"))
        , testCase
              "Minute"
              (assertEqual
                   "Adds a minute"
                   (Right (toTime (2019, 12, 18) 32043))
                   (parseDate time "1m"))
        , testCase
              "Hour"
              (assertEqual
                   "Adds an hour"
                   (Right (toTime (2019, 12, 18) 35583))
                   (parseDate time "1h"))
        , testCase
              "Day"
              (assertEqual "Adds a day" (Right (toTime (2019, 12, 19) 31983)) (parseDate time "1d"))
        , testCase
              "Days"
              (assertEqual
                   "Adds 29 days"
                   (Right (toTime (2020, 1, 16) 31983))
                   (parseDate time "29 d"))
        , testCase
              "Week"
              (assertEqual "Adds a week" (Right (toTime (2019, 12, 25) 31983)) (parseDate time "1w"))
        , testCase
              "Mix"
              (assertEqual
                   "Adds 1 week 2 days and 29 seconds"
                   (Right (toTime (2019, 12, 27) 32012))
                   (parseDate time " 1 w 2d 29 s "))
        , testCase
              "Mix out of order"
              (assertEqual
                   "Adds 1 week 2 days and 29 seconds"
                   (Right (toTime (2019, 12, 27) 32012))
                   (parseDate time " 2d 1 w 29 s"))
        , testCase
              "invalid format"
              (assertEqual "Error" (Left "Could not parse date.") (parseDate time "18/12/2019"))
        , testCase
              "invalid numbers"
              (assertEqual "Error" (Left "Could not parse date.") (parseDate time "2019-39-59"))
        ]
